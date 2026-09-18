using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class InvasionOwnerEvacuationService :
    IInvasionOwnerEvacuationService,
    IDungeonSaveRestoreCompletedHook,
    IInitializable,
    ITickable,
    IDisposable
{
    private const string ResidentIntentOwnerPrefix = "invasion:resident-evacuation:";
    private const int AutoRequestPlannerTicks = 2;
    private const int ResidentPathPendingFramesPerAttempt = 180;
    private const int ResidentMovementAttempts = 3;
    private const int ResidentTargetCandidateLimit = 3;

    private readonly IInvasionIntruderContext invasionContext;
    private readonly InvasionDirectorRuntime director;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IGameEventBus gameEventBus;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private readonly ICharacterCombatStanceQuery combatStance;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IDefenseEngagementStore defenseEngagements;
    private readonly IEnvironmentalFireQuery environmentalFires;
    private readonly IWorldHazardZoneQuery worldHazards;
    private readonly Dictionary<string, ResidentRuntime> residents =
        new(StringComparer.Ordinal);
    private readonly HashSet<Vector2Int> observedFireCells = new();
    private IDisposable invasionSpawnedSubscription;
    private CharacterActor owner;
    private Coroutine movementRoutine;
    private bool usedAdministrationRoom;
    private ResidentEvacuationZoneSaveData residentZone = new();
    private Grid residentGrid;
    private RoomInstance residentRoom;
    private int observedResidentStructure = -1;
    private int autoRequestTicksRemaining;
    private RestoreCandidate restoreCandidate;
    private bool restorePublicationPending;
    private bool previousProjectionRetired;
    private bool restoreResumePending;
    private Grid restoreResumeGrid;
    private long nextResidentMovementEpoch;

    private sealed class ResidentRuntime
    {
        internal CharacterActor Actor;
        internal Vector2Int Target;
        internal ResidentEvacuationParticipantStatus Status;
        internal CharacterActionIntentLease Lease;
        internal Coroutine Movement;
        internal long MovementEpoch;
        internal IReadOnlyList<Vector2Int> CandidateTargets =
            Array.Empty<Vector2Int>();
        internal int TargetCandidateIndex;
    }

    private sealed class RestoredResident
    {
        internal CharacterActor Actor;
        internal Vector2Int Target;
        internal ResidentEvacuationParticipantStatus Status;
    }

    private sealed class RestoreCandidate
    {
        internal bool Active;
        internal CharacterActor Owner;
        internal Vector2Int Target;
        internal bool UsedAdministrationRoom;
        internal string StatusText;
        internal ResidentEvacuationZoneSaveData Zone;
        internal Grid Grid;
        internal RoomInstance Room;
        internal readonly List<RestoredResident> Residents = new();
    }

    public InvasionOwnerEvacuationService(
        IInvasionIntruderContext invasionContext,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IRoomLayoutCache roomLayoutCache,
        IGameEventBus gameEventBus,
        ICharacterWorldQuery characterWorld,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        ICharacterSettlementStandingQuery settlementStandings,
        ICharacterCombatStanceQuery combatStance,
        IBuildingWorldQuery buildingWorld,
        IDefenseEngagementStore defenseEngagements,
        IEnvironmentalFireQuery environmentalFires,
        IWorldHazardZoneQuery worldHazards)
    {
        this.invasionContext = invasionContext
            ?? throw new ArgumentNullException(nameof(invasionContext));
        director = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes))).Director
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionOwnerEvacuationService)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
        this.combatStance = combatStance
            ?? throw new ArgumentNullException(nameof(combatStance));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.defenseEngagements = defenseEngagements
            ?? throw new ArgumentNullException(nameof(defenseEngagements));
        this.environmentalFires = environmentalFires
            ?? throw new ArgumentNullException(nameof(environmentalFires));
        this.worldHazards = worldHazards
            ?? throw new ArgumentNullException(nameof(worldHazards));
    }

    public bool IsEvacuating { get; private set; }
    public bool HasReachedTarget => owner != null && owner.GetNowXY() == TargetCell;
    public CharacterActor Owner => owner;
    public Vector2Int TargetCell { get; private set; }
    public string StatusText { get; private set; } = string.Empty;
    public ResidentEvacuationZoneStatus ResidentZoneStatus => residentZone.status;
    public string ResidentEvacuationStatusText => BuildResidentStatusText();
    public IReadOnlyList<ResidentEvacuationParticipantView>
        ResidentEvacuationParticipants => residents
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => BuildResidentParticipantView(pair.Key, pair.Value))
            .ToArray();

    public void Initialize()
    {
        invasionSpawnedSubscription =
            gameEventBus.Subscribe<InvasionSpawnedEvent>(OnInvasionSpawned);
    }

    public void Dispose()
    {
        invasionSpawnedSubscription?.Dispose();
        invasionSpawnedSubscription = null;
        ClearPendingRestoreResume();
        restoreCandidate = null;
        restorePublicationPending = false;
        previousProjectionRetired = false;
        observedFireCells.Clear();
        ReleaseOwner();
        ReleaseResidents(clear: true);
    }

    public void Tick()
    {
        // Bare section-registry restore does not publish a playable world. Only
        // DungeonGameSaveService invokes the completed hook after every world
        // participant has activated its replacement objects.
        if (restoreResumePending)
        {
            return;
        }

        bool invasionThreatActive = director.ActiveIntruders.Count > 0;
        bool fireThreatActive = RefreshObservedFireCells();
        if (!invasionThreatActive && IsEvacuating)
        {
            ReleaseOwner();
        }
        if (!invasionThreatActive && !fireThreatActive)
        {
            autoRequestTicksRemaining = 0;
            if (residents.Count > 0) ReleaseResidents(clear: true);
            return;
        }

        ValidateResidentTopology();
        if (autoRequestTicksRemaining > 0 && --autoRequestTicksRemaining == 0)
        {
            TryRequestResidentEvacuation(out _);
        }
        foreach (KeyValuePair<string, ResidentRuntime> pair
                 in residents.ToArray())
        {
            TickResident(pair.Key, pair.Value);
        }
    }

    private void OnInvasionSpawned(InvasionSpawnedEvent eventType)
    {
        BeginEvacuation();
        if (residentZone.status == ResidentEvacuationZoneStatus.Active)
        {
            // The defense planner owns guard assignment on its entry point. A
            // bounded two-tick handoff lets it claim guards before this shared
            // command evaluates the remaining noncombat residents.
            autoRequestTicksRemaining = AutoRequestPlannerTicks;
        }
    }

    private bool RefreshObservedFireCells()
    {
        HashSet<Vector2Int> current = environmentalFires
            .GetActiveFireCells()
            .ToHashSet();
        if (!observedFireCells.SetEquals(current))
        {
            observedFireCells.Clear();
            observedFireCells.UnionWith(current);
            if (current.Count > 0
                && residentZone.status == ResidentEvacuationZoneStatus.Active)
            {
                // Give the fire hazard overlay one full tick to publish before
                // selecting the room's safe holding cells.
                autoRequestTicksRemaining = AutoRequestPlannerTicks;
            }
        }
        return current.Count > 0;
    }

    private bool HasResidentThreat() =>
        director.ActiveIntruders.Count > 0
        || environmentalFires.ActiveFires.Count > 0;

    public bool IsResidentEvacuationRoom(Grid grid, RoomInstance room) =>
        grid != null
        && room != null
        && residentZone.status == ResidentEvacuationZoneStatus.Active
        && ReferenceEquals(grid, residentGrid)
        && RoomMatchesZone(room, residentZone);

    public bool TryDesignateResidentEvacuationRoom(
        Grid grid,
        RoomInstance room,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (grid == null || room == null)
        {
            failureReason = "대피 구역으로 지정할 방이 없습니다.";
            return false;
        }
        RoomLayout layout = roomLayoutCache.GetLayout(grid);
        if (!room.IsUsable || room.IsSelfContained || room.Cells.Count == 0)
        {
            failureReason = "폐쇄되어 출입문이 있는 일반 방만 대피 구역으로 지정할 수 있습니다.";
            return false;
        }
        if (layout == null
            || !layout.Rooms.Any(candidate => ReferenceEquals(candidate, room)))
        {
            failureReason = "현재 구조에 속한 방만 대피 구역으로 지정할 수 있습니다.";
            return false;
        }

        ReleaseResidents(clear: true);
        residentZone = BuildZone(room);
        residentGrid = grid;
        residentRoom = room;
        observedResidentStructure = grid.StructuralVersion;
        autoRequestTicksRemaining = HasResidentThreat() ? 1 : 0;
        return true;
    }

    public bool ClearResidentEvacuationRoom(out string failureReason)
    {
        failureReason = string.Empty;
        if (residentZone.status == ResidentEvacuationZoneStatus.None)
        {
            failureReason = "해제할 주민 대피 구역이 없습니다.";
            return false;
        }
        ReleaseResidents(clear: true);
        residentZone = new ResidentEvacuationZoneSaveData();
        residentGrid = null;
        residentRoom = null;
        observedResidentStructure = -1;
        autoRequestTicksRemaining = 0;
        return true;
    }

    public bool TryRequestResidentEvacuation(out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasResidentThreat())
        {
            failureReason = "현재 침입 또는 화재 위협이 없어 주민 대피 명령을 내릴 수 없습니다.";
            return false;
        }
        if (!TryResolveCurrentRoom(out Grid grid, out RoomInstance room))
        {
            failureReason = residentZone.status == ResidentEvacuationZoneStatus.LostByTopology
                ? "구조 변경으로 주민 대피 구역을 찾을 수 없습니다. 지정을 해제하거나 다시 지정하세요."
                : "주민 대피 구역을 먼저 지정하세요.";
            return false;
        }

        autoRequestTicksRemaining = 0;
        Dictionary<string, Vector2Int> previousTargets = residents
            .Where(pair => pair.Value != null
                && HasTarget(pair.Value.Status))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Target,
                StringComparer.Ordinal);
        ReleaseResidents(clear: true);
        HashSet<Vector2Int> claimedTargets = new();
        int eligibleCount = 0;
        foreach (CharacterActor actor in characterWorld.Characters
            .Where(IsEligibleResident)
            .OrderBy(GetCharacterId, StringComparer.Ordinal))
        {
            eligibleCount++;
            ResidentRuntime runtime = new()
            {
                Actor = actor,
                Status = ResidentEvacuationParticipantStatus.PendingRoute
            };
            residents.Add(GetCharacterId(actor), runtime);

            previousTargets.TryGetValue(
                GetCharacterId(actor),
                out Vector2Int previousTarget);
            Vector2Int[] available = BuildResidentTargetCandidates(
                actor,
                grid,
                room,
                previousTargets.ContainsKey(GetCharacterId(actor))
                    ? previousTarget
                    : null,
                claimedTargets);
            runtime.CandidateTargets = available;
            if (available.Length == 0)
            {
                runtime.Status = ResidentEvacuationParticipantStatus.NoCapacity;
                continue;
            }

            runtime.Target = available[0];
            claimedTargets.Add(runtime.Target);
            BeginOrResumeResident(runtime);
        }

        if (eligibleCount == 0)
        {
            failureReason = "대피 대상인 일반 직원 또는 비전투 하수인이 없습니다.";
            return false;
        }
        int routed = residents.Values.Count(runtime => HasTarget(runtime.Status));
        gameEventBus.RaiseAlert(
            "주민 대피 명령",
            $"대상 {eligibleCount}명 중 {routed}명이 지정 구역으로 이동합니다.",
            routed > 0 ? EventAlertImportance.High : EventAlertImportance.Medium,
            "방어");
        if (routed == 0)
        {
            failureReason = "대피 구역에 도달 가능한 빈 안전 칸이 없습니다.";
            return false;
        }
        return true;
    }

    public OwnerEvacuationSaveSnapshot Capture()
    {
        ResidentEvacuationZoneSaveData capturedZone = CloneZone(residentZone);
        List<ResidentEvacuationParticipantSaveData> capturedResidents = residents
            .Where(pair => IsCapturableResident(pair.Key, pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => CaptureResident(pair.Key, pair.Value))
            .ToList();
        if (capturedZone.status == ResidentEvacuationZoneStatus.Active
            && (residentGrid == null
                || !TryFindMatchingRoom(residentGrid, capturedZone, out _)))
        {
            capturedZone.status = ResidentEvacuationZoneStatus.LostByTopology;
            capturedResidents.Clear();
        }
        if (!HasResidentThreat())
        {
            capturedResidents.Clear();
        }
        return new OwnerEvacuationSaveSnapshot
        {
            active = IsEvacuating,
            targetX = TargetCell.x,
            targetY = TargetCell.y,
            usedAdministrationRoom = usedAdministrationRoom,
            statusText = StatusText,
            residentZone = capturedZone,
            residentParticipants = capturedResidents
        };
    }

    public void PrepareRestoreCandidate(
        OwnerEvacuationSaveSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (restoreCandidate != null || restoreResumePending)
        {
            report.AddError("An owner evacuation restore candidate is already prepared.");
            return;
        }
        if (snapshot == null)
        {
            report.AddError("Owner evacuation restore payload is null.");
            return;
        }

        if (!restoreWorldCandidates.TryGetGrid(out Grid candidateGrid)
            || candidateGrid == null
            || !restoreWorldCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> candidateCharacters)
            || candidateCharacters == null)
        {
            report.AddError(
                "Owner evacuation restore requires the staged facility and character world candidates.");
            return;
        }

        RestoreCandidate candidate = new()
        {
            Active = snapshot.active,
            Target = new Vector2Int(snapshot.targetX, snapshot.targetY),
            UsedAdministrationRoom = snapshot.usedAdministrationRoom,
            StatusText = snapshot.statusText,
            Zone = CloneZone(snapshot.residentZone),
            Grid = candidateGrid
        };
        if (snapshot.active)
        {
            CharacterActor[] candidateOwners = candidateCharacters
                .Where(actor => actor != null && actor.IsOwner)
                .ToArray();
            if (candidateOwners.Length != 1 || candidateOwners[0].IsDead)
            {
                report.AddError(
                    "Owner evacuation restore requires exactly one living staged owner.");
                return;
            }
            if (!candidateGrid.IsValidGridPos(candidate.Target)
                || !candidateGrid.IsWalkable(candidate.Target))
            {
                report.AddError("Owner evacuation restore target is not a valid walkable cell.");
                return;
            }
            candidate.Owner = candidateOwners[0];
        }

        if (candidate.Zone.status != ResidentEvacuationZoneStatus.None)
        {
            if (candidate.Zone.status == ResidentEvacuationZoneStatus.Active
                && !TryFindMatchingRoom(
                    candidateGrid,
                    candidate.Zone,
                    out candidate.Room))
            {
                report.AddError("Resident evacuation restore zone does not join the staged room topology.");
                return;
            }
        }

        Dictionary<string, CharacterActor> actors = candidateCharacters
            .Where(actor => actor != null
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .ToDictionary(GetCharacterId, StringComparer.Ordinal);
        HashSet<Vector2Int> structurallyUnavailable = candidate.Room != null
            ? BuildStructurallyUnavailableCells(candidate.Grid, candidate.Room)
            : new HashSet<Vector2Int>();
        foreach (ResidentEvacuationParticipantSaveData participant in snapshot.residentParticipants)
        {
            if (!actors.TryGetValue(participant.characterId, out CharacterActor actor)
                || !IsPersistentResidentActor(actor))
            {
                report.AddError(
                    $"Resident evacuation participant '{participant.characterId}' "
                    + "does not join a staged living resident.");
                return;
            }
            Vector2Int target = new(participant.targetX, participant.targetY);
            if (HasTarget(participant.status)
                && (candidate.Room == null
                    || !candidate.Room.ContainsCell(target)
                    || !candidate.Grid.IsWalkable(target)
                    || structurallyUnavailable.Contains(target)))
            {
                report.AddError(
                    $"Resident evacuation participant '{participant.characterId}' "
                    + "does not join a staged walkable room cell.");
                return;
            }
            candidate.Residents.Add(new RestoredResident
            {
                Actor = actor,
                Target = target,
                Status = participant.status
            });
        }
        restoreCandidate = candidate;
    }

    public void PublishRestoreCandidate()
    {
        if (restoreCandidate == null || restorePublicationPending)
            throw new InvalidOperationException("No owner evacuation restore candidate is ready to publish.");
        restorePublicationPending = true;
        previousProjectionRetired = false;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        ClearPendingRestoreResume();
        restoreCandidate = null;
        restorePublicationPending = false;
        previousProjectionRetired = false;
    }

    public void RetirePreviousRestoreProjection()
    {
        if (!restorePublicationPending || previousProjectionRetired)
            throw new InvalidOperationException("No owner evacuation projection is ready to retire.");
        ReleaseOwner();
        ReleaseResidents(clear: true);
        previousProjectionRetired = true;
    }

    public void ActivateRestoreProjection()
    {
        if (!restorePublicationPending || !previousProjectionRetired)
            throw new InvalidOperationException("The previous owner evacuation projection was not retired.");

        RestoreCandidate candidate = restoreCandidate;
        restoreCandidate = null;
        restorePublicationPending = false;
        previousProjectionRetired = false;
        ApplyRestoredResidents(candidate);
        restoreResumeGrid = candidate.Grid;
        restoreResumePending = true;
        if (!candidate.Active)
        {
            TargetCell = candidate.Target;
            usedAdministrationRoom = candidate.UsedAdministrationRoom;
            StatusText = candidate.StatusText ?? string.Empty;
            return;
        }
        owner = candidate.Owner;
        TargetCell = candidate.Target;
        usedAdministrationRoom = candidate.UsedAdministrationRoom;
        StatusText = candidate.StatusText ?? string.Empty;
        IsEvacuating = true;
    }

    public void CompleteRestoreCandidate()
    {
        RetirePreviousRestoreProjection();
        ActivateRestoreProjection();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublicationPending)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ClearPendingRestoreResume();
        restoreCandidate = null;
        previousProjectionRetired = false;
    }

    public void OnRestoreCompleted()
    {
        if (!restoreResumePending)
        {
            return;
        }

        Grid expectedGrid = restoreResumeGrid;
        ClearPendingRestoreResume();
        if (restoreWorldCandidates.TryGetGrid(out _)
            || restoreWorldCandidates.TryGetCharacters(out _))
        {
            throw new InvalidOperationException(
                "Owner evacuation restore completed before staged world candidates were retired.");
        }
        if (!invasionContext.TryGetGrid(out Grid liveGrid)
            || liveGrid == null
            || !ReferenceEquals(liveGrid, expectedGrid))
        {
            throw new InvalidOperationException(
                "Owner evacuation restore did not join the published facility grid.");
        }

        Dictionary<string, CharacterActor> liveActors = characterWorld.Characters
            .Where(actor => actor != null
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .ToDictionary(GetCharacterId, StringComparer.Ordinal);
        ValidateRestoredActorJoin(owner, liveActors, "owner");
        if (IsEvacuating
            && (!invasionContext.TryGetOwner(out CharacterActor liveOwner)
                || !ReferenceEquals(liveOwner, owner)))
        {
            throw new InvalidOperationException(
                "Owner evacuation restore did not join the published owner authority.");
        }
        foreach (KeyValuePair<string, ResidentRuntime> pair in residents)
        {
            ValidateRestoredActorJoin(pair.Value?.Actor, liveActors, pair.Key);
        }

        if (residentZone.status != ResidentEvacuationZoneStatus.None)
        {
            residentGrid = liveGrid;
            if (residentZone.status == ResidentEvacuationZoneStatus.Active
                && !TryFindMatchingRoom(
                    liveGrid,
                    residentZone,
                    out residentRoom))
            {
                throw new InvalidOperationException(
                    "Owner evacuation restore zone did not join the published room topology.");
            }
            observedResidentStructure = liveGrid.StructuralVersion;
        }

        foreach (ResidentRuntime runtime in residents.Values)
        {
            runtime.CandidateTargets = residentRoom != null
                ? BuildResidentTargetCandidates(
                    runtime.Actor,
                    liveGrid,
                    residentRoom,
                    runtime.Target,
                    new HashSet<Vector2Int>(residents.Values
                        .Where(item => item != null
                            && !ReferenceEquals(item, runtime)
                            && HasTarget(item.Status))
                        .Select(item => item.Target)))
                : Array.Empty<Vector2Int>();
            if (HasResidentThreat() && HasTarget(runtime.Status))
            {
                BeginOrResumeResident(runtime);
            }
        }

        if (IsEvacuating)
        {
            StartOwnerMovement(TargetCell, StatusText);
        }
    }

    private void ApplyRestoredResidents(RestoreCandidate candidate)
    {
        residentZone = CloneZone(candidate.Zone);
        residentGrid = candidate.Zone.status != ResidentEvacuationZoneStatus.None
            ? candidate.Grid
            : null;
        residentRoom = candidate.Room;
        observedResidentStructure = -1;
        observedFireCells.Clear();
        foreach (RestoredResident restored in candidate.Residents)
        {
            ResidentRuntime runtime = new()
            {
                Actor = restored.Actor,
                Target = restored.Target,
                Status = restored.Status,
                CandidateTargets = Array.Empty<Vector2Int>()
            };
            residents.Add(GetCharacterId(restored.Actor), runtime);
        }
    }

    private static void ValidateRestoredActorJoin(
        CharacterActor actor,
        IReadOnlyDictionary<string, CharacterActor> liveActors,
        string role)
    {
        if (actor == null)
        {
            if (string.Equals(role, "owner", StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Owner evacuation restored participant '{role}' is missing.");
        }

        string actorId = GetCharacterId(actor);
        if (!actor.gameObject.activeInHierarchy
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active
            || liveActors == null
            || !liveActors.TryGetValue(actorId, out CharacterActor published)
            || !ReferenceEquals(published, actor))
        {
            throw new InvalidOperationException(
                $"Owner evacuation restored {role} '{actorId}' did not join the active character world.");
        }
    }

    private void ClearPendingRestoreResume()
    {
        restoreResumePending = false;
        restoreResumeGrid = null;
    }

    private void TickResident(string actorId, ResidentRuntime runtime)
    {
        CharacterActor actor = runtime?.Actor;
        if (actor == null || actor.IsDead
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            ReleaseResident(runtime);
            residents.Remove(actorId);
            return;
        }
        if (!IsEligibleResident(actor))
        {
            ReleaseResident(runtime);
            residents.Remove(actorId);
            return;
        }
        AIBrain brain = actor.Brain;
        if (runtime.Lease.IsValid
            && (brain == null || !brain.IsExternalIntentCurrent(runtime.Lease)))
        {
            runtime.Lease = default;
            runtime.Status = ResidentEvacuationParticipantStatus.PreemptedEmergency;
        }
        if (runtime.Status == ResidentEvacuationParticipantStatus.Holding
            && actor.GetNowXY() != runtime.Target)
        {
            runtime.Status = ResidentEvacuationParticipantStatus.PendingRoute;
        }
        if ((runtime.Status == ResidentEvacuationParticipantStatus.PreemptedEmergency
                || runtime.Status == ResidentEvacuationParticipantStatus.PendingRoute)
            && runtime.Movement == null)
        {
            BeginOrResumeResident(runtime);
        }
    }

    private void BeginOrResumeResident(ResidentRuntime runtime)
    {
        CharacterActor actor = runtime?.Actor;
        AIBrain brain = actor != null ? actor.Brain : null;
        AbilityMove move = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (actor == null || actor.IsDead || brain == null || move == null)
        {
            if (runtime != null)
            {
                runtime.Status = ResidentEvacuationParticipantStatus.Unreachable;
                runtime.Target = default;
            }
            return;
        }

        string actorId = GetCharacterId(actor);
        CharacterActionIntentLease lease = runtime.Lease;
        if (!lease.IsValid || !brain.IsExternalIntentCurrent(lease))
        {
            if (!brain.TryBeginExternallyDrivenAction(
                    ResidentIntentOwnerPrefix + actorId,
                    CharacterActionIntentKind.RoutineNeed,
                    "주민 대피",
                    actor.GetNowXY() == runtime.Target ? "Holding" : "PendingRoute",
                    $"지정 대피 구역 {runtime.Target}",
                    out lease))
            {
                runtime.Lease = default;
                runtime.Status = ResidentEvacuationParticipantStatus.PreemptedEmergency;
                return;
            }
            runtime.Lease = lease;
        }

        if (actor.GetNowXY() == runtime.Target)
        {
            runtime.Status = ResidentEvacuationParticipantStatus.Holding;
            brain.UpdateExternallyDrivenAction(
                lease,
                "주민 대피",
                "Holding",
                $"지정 대피 구역 {runtime.Target}");
            return;
        }

        runtime.Status = ResidentEvacuationParticipantStatus.PendingRoute;
        if (runtime.Movement == null)
        {
            long movementEpoch = checked(++nextResidentMovementEpoch);
            runtime.MovementEpoch = movementEpoch;
            Coroutine movement = actor.StartCoroutine(RunResident(
                runtime,
                lease,
                movementEpoch,
                runtime.Target));
            if (runtime.MovementEpoch == movementEpoch)
            {
                runtime.Movement = movement;
            }
        }
    }

    private IEnumerator RunResident(
        ResidentRuntime runtime,
        CharacterActionIntentLease runLease,
        long runEpoch,
        Vector2Int runTarget)
    {
        CharacterActor actor = runtime.Actor;
        AIBrain brain = actor != null ? actor.Brain : null;
        AbilityMove move = actor != null ? actor.GetAbility<AbilityMove>() : null;
        IGridPathSearchBroker pathSearch = actor != null
            ? actor.PathSearchBroker
            : null;
        if (actor == null || brain == null || move == null || pathSearch == null
            || !invasionContext.TryGetGrid(out Grid grid))
        {
            FailResidentRunIfCurrent(
                runtime,
                brain,
                runLease,
                runEpoch,
                runTarget,
                clearTarget: true);
            yield break;
        }

        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor),
            movementIntent: GridMovementIntent.EscapeHazard);
        for (int moveAttempt = 0;
             moveAttempt < ResidentMovementAttempts && !actor.IsDead;
             moveAttempt++)
        {
            if (!IsResidentRunCurrent(
                    runtime,
                    brain,
                    runLease,
                    runEpoch,
                    runTarget))
            {
                RetireResidentMovementRun(runtime, runEpoch);
                yield break;
            }

            if (actor.GetNowXY() == runTarget)
            {
                break;
            }

            Queue<GridMoveStep> path = null;
            GridPathRequestStatus pathStatus = GridPathRequestStatus.Pending;
            for (int pendingFrame = 0;
                 pendingFrame < ResidentPathPendingFramesPerAttempt;
                 pendingFrame++)
            {
                if (!IsResidentRunCurrent(
                        runtime,
                        brain,
                        runLease,
                        runEpoch,
                        runTarget))
                {
                    RetireResidentMovementRun(runtime, runEpoch);
                    yield break;
                }

                pathStatus = pathSearch.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    runTarget,
                    out path,
                    GridPathSearchPriority.Normal,
                    traversal);
                if (pathStatus != GridPathRequestStatus.Pending)
                {
                    break;
                }

                runtime.Status = ResidentEvacuationParticipantStatus.PendingRoute;
                brain.UpdateExternallyDrivenAction(
                    runLease,
                    "주민 대피",
                    "PendingRoute",
                    $"경로 계산 대기 {runTarget}");
                yield return null;
            }

            if (pathStatus == GridPathRequestStatus.Pending)
            {
                // Pending is not a no-path result. Retire this bounded polling
                // run and let Tick resume the same exact request next frame.
                RetireResidentMovementRun(runtime, runEpoch);
                yield break;
            }

            if (pathStatus == GridPathRequestStatus.Unreachable
                || path == null
                || path.Count == 0)
            {
                if (actor.GetNowXY() == runTarget)
                {
                    break;
                }
                if (TryAdvanceResidentTarget(runtime, runTarget))
                {
                    runtime.Status = ResidentEvacuationParticipantStatus.PendingRoute;
                    brain.UpdateExternallyDrivenAction(
                        runLease,
                        "주민 대피",
                        "PendingRoute",
                        $"다음 안전 칸 경로 계산 대기 {runtime.Target}");
                    RetireResidentMovementRun(runtime, runEpoch);
                    yield break;
                }

                FailResidentRunIfCurrent(
                    runtime,
                    brain,
                    runLease,
                    runEpoch,
                    runTarget,
                    clearTarget: true);
                yield break;
            }

            runtime.Status = ResidentEvacuationParticipantStatus.Moving;
            brain.UpdateExternallyDrivenAction(
                runLease,
                "주민 대피",
                "Moving",
                $"지정 대피 구역 {runTarget}");
            yield return move.MoveByPath(path);
        }

        if (!IsResidentRunCurrent(
                runtime,
                brain,
                runLease,
                runEpoch,
                runTarget))
        {
            RetireResidentMovementRun(runtime, runEpoch);
            yield break;
        }

        if (!actor.IsDead && actor.GetNowXY() == runTarget)
        {
            RetireResidentMovementRun(runtime, runEpoch);
            runtime.Status = ResidentEvacuationParticipantStatus.Holding;
            brain.UpdateExternallyDrivenAction(
                runLease, "주민 대피", "Holding", $"지정 대피 구역 {runTarget}");
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Completed,
                "주민 대피 구역 도착",
                actionId: "invasion:resident-evacuation",
                sentiment: -0.1f,
                bubbleEligible: true));
            yield break;
        }

        FailResidentRunIfCurrent(
            runtime,
            brain,
            runLease,
            runEpoch,
            runTarget,
            clearTarget: true);
    }

    private bool TryAdvanceResidentTarget(
        ResidentRuntime runtime,
        Vector2Int failedTarget)
    {
        CharacterActor actor = runtime?.Actor;
        if (actor == null
            || residentGrid == null
            || residentRoom == null
            || residentZone.status != ResidentEvacuationZoneStatus.Active)
        {
            return false;
        }

        HashSet<Vector2Int> claimed = residents.Values
            .Where(other => other != null
                && !ReferenceEquals(other, runtime)
                && HasTarget(other.Status))
            .Select(other => other.Target)
            .ToHashSet();
        IReadOnlyList<Vector2Int> candidates = runtime.CandidateTargets
            ?? Array.Empty<Vector2Int>();
        HashSet<Vector2Int> unavailable = BuildUnavailableCells(
            residentGrid,
            residentRoom,
            actor);
        for (int index = runtime.TargetCandidateIndex + 1;
             index < candidates.Count;
             index++)
        {
            Vector2Int candidate = candidates[index];
            if (candidate == failedTarget
                || claimed.Contains(candidate)
                || unavailable.Contains(candidate)
                || !IsValidHoldingCell(residentGrid, residentRoom, candidate))
            {
                continue;
            }

            runtime.Target = candidate;
            runtime.TargetCandidateIndex = index;
            return true;
        }
        return false;
    }

    private static bool IsResidentRunCurrent(
        ResidentRuntime runtime,
        AIBrain brain,
        in CharacterActionIntentLease runLease,
        long runEpoch,
        Vector2Int runTarget) =>
        runtime != null
        && runtime.MovementEpoch == runEpoch
        && runtime.Target == runTarget
        && SameLease(runtime.Lease, runLease)
        && brain != null
        && brain.IsExternalIntentCurrent(runLease);

    private static void RetireResidentMovementRun(
        ResidentRuntime runtime,
        long runEpoch)
    {
        if (runtime == null || runtime.MovementEpoch != runEpoch)
        {
            return;
        }
        runtime.Movement = null;
        runtime.MovementEpoch = 0L;
    }

    private static void FailResidentRunIfCurrent(
        ResidentRuntime runtime,
        AIBrain brain,
        in CharacterActionIntentLease runLease,
        long runEpoch,
        Vector2Int runTarget,
        bool clearTarget)
    {
        if (!IsResidentRunCurrent(
                runtime,
                brain,
                runLease,
                runEpoch,
                runTarget))
        {
            RetireResidentMovementRun(runtime, runEpoch);
            return;
        }

        RetireResidentMovementRun(runtime, runEpoch);
        runtime.Status = ResidentEvacuationParticipantStatus.Unreachable;
        brain.FailExternallyDrivenAction(runLease);
        if (SameLease(runtime.Lease, runLease))
        {
            runtime.Lease = default;
        }
        if (clearTarget)
        {
            runtime.Target = default;
        }
    }

    private void ValidateResidentTopology()
    {
        if (residentZone.status != ResidentEvacuationZoneStatus.Active
            || residentGrid == null
            || residentGrid.StructuralVersion == observedResidentStructure)
            return;

        observedResidentStructure = residentGrid.StructuralVersion;
        if (TryFindMatchingRoom(residentGrid, residentZone, out RoomInstance matched))
        {
            residentRoom = matched;
            HashSet<Vector2Int> unavailable =
                BuildStructurallyUnavailableCells(residentGrid, matched);
            foreach (ResidentRuntime runtime in residents.Values)
            {
                if (!HasTarget(runtime.Status)
                    || IsValidHoldingCell(residentGrid, matched, runtime.Target)
                        && !unavailable.Contains(runtime.Target))
                {
                    continue;
                }
                ReleaseResident(runtime);
                runtime.Target = default;
                runtime.Status = ResidentEvacuationParticipantStatus.NoCapacity;
            }
            return;
        }
        ReleaseResidents(clear: true);
        residentZone.status = ResidentEvacuationZoneStatus.LostByTopology;
        residentRoom = null;
        autoRequestTicksRemaining = 0;
    }

    private bool TryResolveCurrentRoom(out Grid grid, out RoomInstance room)
    {
        ValidateResidentTopology();
        grid = residentGrid;
        room = residentRoom;
        if (residentZone.status != ResidentEvacuationZoneStatus.Active || grid == null)
        {
            room = null;
            return false;
        }
        if (room != null && RoomMatchesZone(room, residentZone)) return true;
        return TryFindMatchingRoom(grid, residentZone, out room);
    }

    private bool TryFindMatchingRoom(
        Grid grid,
        ResidentEvacuationZoneSaveData zone,
        out RoomInstance room)
    {
        room = null;
        if (grid == null || zone == null) return false;
        room = roomLayoutCache.GetLayout(grid).Rooms.FirstOrDefault(candidate =>
            candidate != null && candidate.IsUsable && !candidate.IsSelfContained
            && RoomMatchesZone(candidate, zone));
        return room != null;
    }

    private Vector2Int[] BuildResidentTargetCandidates(
        CharacterActor actor,
        Grid grid,
        RoomInstance room,
        Vector2Int? previousTarget,
        HashSet<Vector2Int> claimedTargets)
    {
        if (actor == null || grid == null || room == null)
        {
            return Array.Empty<Vector2Int>();
        }

        HashSet<Vector2Int> unavailable = BuildUnavailableCells(
            grid,
            room,
            actor);
        Vector2Int current = actor.GetNowXY();
        return room.Cells
            .Distinct()
            .Where(cell => IsValidHoldingCell(grid, room, cell)
                && !unavailable.Contains(cell)
                && (claimedTargets == null || !claimedTargets.Contains(cell)))
            .OrderBy(cell => previousTarget.HasValue
                && cell == previousTarget.Value ? 0
                : cell == current ? 1 : 2)
            .ThenBy(cell => Manhattan(current, cell))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .Take(ResidentTargetCandidateLimit)
            .ToArray();
    }

    private HashSet<Vector2Int> BuildUnavailableCells(
        Grid grid,
        RoomInstance room,
        CharacterActor subject)
    {
        HashSet<Vector2Int> unavailable =
            BuildStructurallyUnavailableCells(grid, room);
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor != null && !actor.IsDead && actor != subject)
            {
                unavailable.Add(actor.GetNowXY());
            }
        }
        foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders)
        {
            if (intruder?.IntruderActor != null && !intruder.IntruderActor.IsDead)
            {
                unavailable.Add(intruder.IntruderActor.GetNowXY());
            }
        }
        CharacterId subjectId = CharacterPersistentIdentity.Require(subject);
        foreach (Vector2Int cell in room.Cells)
        {
            if ((worldHazards.GetHazard(subjectId, cell).Flags
                    & WorldHazardFlags.Fire) != 0)
            {
                unavailable.Add(cell);
            }
        }
        return unavailable;
    }

    private HashSet<Vector2Int> BuildStructurallyUnavailableCells(
        Grid grid,
        RoomInstance room)
    {
        HashSet<Vector2Int> unavailable = new();
        foreach (BuildableObject door in room.Doors)
        {
            if (door == null || door.isDestroy) continue;
            foreach (Vector2Int cell in door.buildPoses) unavailable.Add(cell);
            unavailable.Add(door.centerPos);
        }
        if (invasionContext.TryResolveEntry(out InvasionIntruderEntry entry))
            unavailable.Add(entry.GridPosition);

        foreach (BuildableObject building in buildingWorld.Buildings)
        {
            if (building == null || building.isDestroy || building.Grid != grid
                || building.BuildingData == null) continue;
            IReadOnlyList<Vector2Int> footprint = building.buildPoses.Count > 0
                ? building.buildPoses
                : building.BuildingData.GetGridPosList(building.centerPos);

            // Movement surfaces and decorative/non-operational fixtures do not
            // reserve an entire room. Only truly exclusive walkable facility
            // footprints and one deterministic operational access cell do.
            if (building.BlocksGridMovement
                || building.Facility != null
                    && building.AllowsInteriorWalkability
                    && !building.IsGridMovement)
            {
                foreach (Vector2Int occupied in footprint)
                {
                    unavailable.Add(occupied);
                }
            }

            bool hasPrimaryOperation = building.Facility?.SupportedWorkTypeIds
                .Any(workType => workType != BuiltInWorkTypeIds.Clean
                    && workType != BuiltInWorkTypeIds.Repair) == true;
            if (!hasPrimaryOperation)
            {
                continue;
            }

            Vector2Int? operationalAccess = BuildingWorkAccessRules
                .EnumerateCandidates(footprint, building.IsGridMovement)
                .Where(cell => room.ContainsCell(cell)
                    && grid.IsValidGridPos(cell)
                    && grid.IsWalkable(cell))
                .OrderBy(cell => Manhattan(cell, building.centerPos))
                .ThenBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .Cast<Vector2Int?>()
                .FirstOrDefault();
            if (operationalAccess.HasValue)
            {
                unavailable.Add(operationalAccess.Value);
            }
        }
        return unavailable;
    }

    private bool IsEligibleResident(CharacterActor actor)
    {
        if (!IsPersistentResidentActor(actor)
            || !actor.gameObject.activeInHierarchy
            || actor.IsOwner
            || IsAssignedGuard(actor)
            || combatStance.IsInCombatStance(actor)
            || actor.GetComponent<AbilityRescue>()?.IsRescuing == true)
            return false;
        AbilityWork work = actor.GetAbility<AbilityWork>();
        if (IsProtectedActiveWork(work))
            return false;
        return work == null
            || (!work.HasEmergencyResponseWorkGateForDiagnostics
                && work.AssignedWorkTypeId != BuiltInWorkTypeIds.Guard
                && work.AssignedWorkTypeId != BuiltInWorkTypeIds.Rescue);
    }

    private static bool IsProtectedActiveWork(AbilityWork work)
    {
        if (work == null || !work.isWorking)
            return false;

        WorkTypeId activeType = work.AssignedWorkTypeId;
        return activeType == BuiltInWorkTypeIds.Treat
            || activeType == BuiltInWorkTypeIds.Surgery
            || activeType == BuiltInWorkTypeIds.ThreatMitigation;
    }

    private bool IsPersistentResidentActor(CharacterActor actor) =>
        actor != null && !actor.IsDead
        && actor.CurrentLifecycleState == CharacterLifecycleState.Active
        && CharacterPersistentIdentity.TryGet(actor, out _)
        && !actor.IsOwner
        && (settlementStandings.IsFormalResident(actor)
            || settlementStandings.IsMinion(actor));

    private bool IsAssignedGuard(CharacterActor actor) =>
        defenseEngagements.Engagements.Any(engagement =>
            engagement != null && engagement.IsActive
            && (engagement.LeadGuard == actor || engagement.ReserveGuard == actor
                || engagement.RangedGuard == actor || engagement.SecondaryRangedGuard == actor));

    private static bool IsValidHoldingCell(Grid grid, RoomInstance room, Vector2Int cell) =>
        room.ContainsCell(cell) && grid.IsValidGridPos(cell) && grid.IsWalkable(cell)
        && grid.GetGridCell(cell)?.AreaType == GridCellAreaType.DungeonInterior;

    private void ReleaseResidents(bool clear)
    {
        foreach (ResidentRuntime runtime in residents.Values)
        {
            ReleaseResident(runtime);
        }
        if (clear) residents.Clear();
    }

    private static void ReleaseResident(ResidentRuntime runtime)
    {
        CharacterActor actor = runtime?.Actor;
        AIBrain brain = actor != null ? actor.Brain : null;
        if (runtime == null)
        {
            return;
        }

        CharacterActionIntentLease releasedLease = runtime.Lease;
        Coroutine releasedMovement = runtime.Movement;
        bool current = brain != null && releasedLease.IsValid
            && brain.IsExternalIntentCurrent(releasedLease);
        runtime.Movement = null;
        runtime.MovementEpoch = 0L;
        runtime.Lease = default;
        if (current)
        {
            if (releasedMovement != null) actor.StopCoroutine(releasedMovement);
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            brain.CancelExternallyDrivenAction(releasedLease);
            brain.RequestImmediateReplan(clearFailures: false);
        }
        else if (releasedMovement != null && actor != null)
        {
            // Stop only the stale evacuation coroutine. Its movement was
            // already cancelled by the higher-priority owner; do not cancel
            // the emergency owner's current AbilityMove operation.
            actor.StopCoroutine(releasedMovement);
        }
    }

    private string BuildResidentStatusText()
    {
        if (residentZone.status == ResidentEvacuationZoneStatus.None)
            return "주민 대피 구역 미지정";
        if (residentZone.status == ResidentEvacuationZoneStatus.LostByTopology)
            return "구조 변경으로 지정 구역을 찾을 수 없음 · 해제 또는 재지정 필요";
        if (residents.Count == 0) return "주민 대피 구역 지정됨 · 침입·화재 시 자동 명령";
        int holding = residents.Values.Count(item => item.Status
            == ResidentEvacuationParticipantStatus.Holding);
        int moving = residents.Values.Count(item => item.Status is
            ResidentEvacuationParticipantStatus.Moving
            or ResidentEvacuationParticipantStatus.PendingRoute);
        int emergency = residents.Values.Count(item => item.Status
            == ResidentEvacuationParticipantStatus.PreemptedEmergency);
        int noPath = residents.Values.Count(item => item.Status
            == ResidentEvacuationParticipantStatus.Unreachable);
        int capacity = residents.Values.Count(item => item.Status
            == ResidentEvacuationParticipantStatus.NoCapacity);
        return $"도착 {holding} · 이동 {moving} · 응급 선점 {emergency} · "
            + $"경로 없음 {noPath} · 수용 불가 {capacity}";
    }

    private static ResidentEvacuationParticipantSaveData CaptureResident(
        string id, ResidentRuntime runtime)
    {
        ResidentEvacuationParticipantStatus status = runtime.Status;
        if (runtime.Lease.IsValid
            && (runtime.Actor?.Brain == null
                || !runtime.Actor.Brain.IsExternalIntentCurrent(runtime.Lease)))
            status = ResidentEvacuationParticipantStatus.PreemptedEmergency;
        return new ResidentEvacuationParticipantSaveData
        {
            characterId = id,
            status = status,
            targetX = runtime.Target.x,
            targetY = runtime.Target.y
        };
    }

    private bool IsCapturableResident(
        string actorId,
        ResidentRuntime runtime)
    {
        CharacterActor actor = runtime?.Actor;
        return actor != null
            && IsEligibleResident(actor)
            && string.Equals(GetCharacterId(actor), actorId, StringComparison.Ordinal);
    }

    private static ResidentEvacuationParticipantView BuildResidentParticipantView(
        string actorId,
        ResidentRuntime runtime)
    {
        CharacterActor actor = runtime?.Actor;
        ResidentEvacuationParticipantStatus status = runtime?.Status
            ?? ResidentEvacuationParticipantStatus.Unreachable;
        bool hasTarget = runtime != null && HasTarget(status);
        string reason = status switch
        {
            ResidentEvacuationParticipantStatus.PendingRoute =>
                "캐릭터 권한을 반영한 경로 계산 대기",
            ResidentEvacuationParticipantStatus.Moving =>
                "지정 대피 구역으로 이동 중",
            ResidentEvacuationParticipantStatus.Holding =>
                "지정 대피 구역에 도착",
            ResidentEvacuationParticipantStatus.PreemptedEmergency =>
                "긴급 생존·의료 행동이 대피를 선점함",
            ResidentEvacuationParticipantStatus.Unreachable =>
                "현재 위치에서 지정 구역까지 도달할 수 없음",
            ResidentEvacuationParticipantStatus.NoCapacity =>
                "출입·필수 접근칸을 제외한 빈 대기 칸이 없음",
            _ => "대피 상태 없음"
        };
        return new ResidentEvacuationParticipantView(
            actorId,
            actor != null
                ? actor.Identity?.DisplayName ?? actor.name
                : actorId,
            status,
            hasTarget,
            runtime?.Target ?? default,
            reason);
    }

    private static bool SameLease(
        in CharacterActionIntentLease left,
        in CharacterActionIntentLease right) =>
        left.Epoch == right.Epoch
        && left.Kind == right.Kind
        && string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal);

    private static ResidentEvacuationZoneSaveData BuildZone(RoomInstance room)
    {
        Vector2Int[] cells = room.Cells.Distinct()
            .OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray();
        ResidentEvacuationZoneSaveData result = new()
        {
            status = ResidentEvacuationZoneStatus.Active,
            anchorX = cells[0].x,
            anchorY = cells[0].y
        };
        int index = 0;
        while (index < cells.Length)
        {
            int y = cells[index].y;
            int start = cells[index].x;
            int end = start;
            index++;
            while (index < cells.Length && cells[index].y == y && cells[index].x == end + 1)
            {
                end = cells[index].x;
                index++;
            }
            result.cellSpans.Add(new ResidentEvacuationRoomCellSpanSaveData
            {
                y = y,
                startX = start,
                length = checked(end - start + 1)
            });
        }
        return result;
    }

    private static ResidentEvacuationZoneSaveData CloneZone(ResidentEvacuationZoneSaveData source)
    {
        source ??= new ResidentEvacuationZoneSaveData();
        return new ResidentEvacuationZoneSaveData
        {
            status = source.status,
            anchorX = source.anchorX,
            anchorY = source.anchorY,
            cellSpans = source.cellSpans?.Where(span => span != null)
                .Select(span => new ResidentEvacuationRoomCellSpanSaveData
                {
                    y = span.y,
                    startX = span.startX,
                    length = span.length
                }).ToList() ?? new List<ResidentEvacuationRoomCellSpanSaveData>()
        };
    }

    private static bool RoomMatchesZone(RoomInstance room, ResidentEvacuationZoneSaveData zone)
    {
        if (room == null || zone?.cellSpans == null) return false;
        ResidentEvacuationZoneSaveData current = BuildZone(room);
        if (current.anchorX != zone.anchorX || current.anchorY != zone.anchorY
            || current.cellSpans.Count != zone.cellSpans.Count) return false;
        for (int index = 0; index < current.cellSpans.Count; index++)
        {
            ResidentEvacuationRoomCellSpanSaveData left = current.cellSpans[index];
            ResidentEvacuationRoomCellSpanSaveData right = zone.cellSpans[index];
            if (right == null || left.y != right.y || left.startX != right.startX
                || left.length != right.length) return false;
        }
        return true;
    }

    private static bool HasTarget(ResidentEvacuationParticipantStatus status) =>
        status is ResidentEvacuationParticipantStatus.PendingRoute
            or ResidentEvacuationParticipantStatus.Moving
            or ResidentEvacuationParticipantStatus.Holding
            or ResidentEvacuationParticipantStatus.PreemptedEmergency;

    private static string GetCharacterId(CharacterActor actor) =>
        CharacterPersistentIdentity.Require(actor).Value;

    private void BeginEvacuation()
    {
        if (!invasionContext.TryGetOwner(out CharacterActor resolvedOwner)
            || resolvedOwner == null || resolvedOwner.IsDead
            || !invasionContext.TryGetGrid(out Grid grid)
            || !TryResolveEvacuationCell(grid, resolvedOwner, out Vector2Int target, out bool administration))
            return;
        owner = resolvedOwner;
        usedAdministrationRoom = administration;
        StartOwnerMovement(target, administration ? "사장실로 대피 중" : "사장실이 없어 임시 대피 중");
        if (!administration)
        {
            gameEventBus.RaiseAlert(
                "사장 임시 대피",
                "사용 가능한 사장실이 없어 입구에서 가장 먼 안전 칸으로 이동합니다.",
                EventAlertImportance.High,
                "방어");
        }
    }

    private void StartOwnerMovement(Vector2Int target, string status)
    {
        if (owner == null) return;
        if (movementRoutine != null) owner.StopCoroutine(movementRoutine);
        owner.GetAbility<AbilityWork>()?.ReleaseAssignedWorkTarget();
        owner.GetAbility<AbilityMove>()?.CancelActiveMovement();
        owner.Brain?.RequestImmediateReplan(clearFailures: false);
        owner.SetAiPaused(true);
        TargetCell = target;
        StatusText = string.IsNullOrWhiteSpace(status) ? "대피 중" : status;
        IsEvacuating = true;
        DefenseCombatPresentation.Ensure(owner)?.SetStatus(GetWorldStatusText(StatusText), false);
        movementRoutine = owner.StartCoroutine(RunEvacuation());
    }

    private IEnumerator RunEvacuation()
    {
        AbilityMove move = owner != null ? owner.GetAbility<AbilityMove>() : null;
        if (move == null || !invasionContext.TryGetGrid(out Grid grid))
        {
            movementRoutine = null;
            yield break;
        }
        for (int attempt = 0; attempt < 3 && owner != null && !owner.IsDead; attempt++)
        {
            if (owner.GetNowXY() == TargetCell) break;
            Queue<GridMoveStep> path = grid.GetMovePathTo(owner.GetNowXY(), TargetCell);
            if (path == null || path.Count == 0) break;
            yield return move.MoveByPath(path);
        }
        if (owner != null && !owner.IsDead && owner.GetNowXY() == TargetCell)
        {
            StatusText = usedAdministrationRoom ? "사장실 대피 완료" : "임시 대피 완료";
            DefenseCombatPresentation.Ensure(owner)?.SetStatus("대피 완료", false);
            owner.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Completed,
                StatusText,
                actionId: "invasion:owner-evacuation",
                sentiment: -0.15f,
                bubbleEligible: true));
        }
        else StatusText = "대피 경로 막힘";
        movementRoutine = null;
    }

    private static string GetWorldStatusText(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "대피 중";
        return status.Contains("사장실이 없어", StringComparison.Ordinal) ? "임시 대피"
            : status.Contains("사장실", StringComparison.Ordinal) ? "사장실 대피"
            : "대피 중";
    }

    private bool TryResolveEvacuationCell(
        Grid grid, CharacterActor targetOwner, out Vector2Int target, out bool administration)
    {
        target = default;
        administration = false;
        GridPathSearchResult search = grid.SearchPath(targetOwner.GetNowXY());
        RoomInstance room = roomLayoutCache.GetLayout(grid).Rooms
            .Where(candidate => candidate != null && candidate.IsUsable
                && (candidate.Roles & FacilityRole.Administration) != 0)
            .OrderByDescending(candidate => candidate.Cells.Count)
            .FirstOrDefault(candidate => candidate.Cells.Any(search.ContainsPosition));
        if (room != null)
        {
            Vector2Int[] doors = room.Doors.Where(door => door != null && !door.isDestroy)
                .Select(door => door.centerPos).ToArray();
            Vector2Int? roomTarget = room.Cells
                .Where(cell => grid.IsWalkable(cell) && search.ContainsPosition(cell))
                .OrderByDescending(cell => DistanceFromNearestDoor(cell, doors))
                .ThenByDescending(cell => cell.y).Cast<Vector2Int?>().FirstOrDefault();
            if (roomTarget.HasValue)
            {
                target = roomTarget.Value;
                administration = true;
                return true;
            }
        }
        Vector2Int entry = Vector2Int.zero;
        if (invasionContext.TryResolveEntry(out InvasionIntruderEntry resolvedEntry))
            entry = resolvedEntry.GridPosition;
        GridCell fallback = search.GetReachablePositions().Select(grid.GetGridCell)
            .Where(cell => cell != null && cell.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Manhattan(cell.Position, entry))
            .ThenByDescending(cell => cell.Position.y).FirstOrDefault();
        if (fallback == null) return false;
        target = fallback.Position;
        return true;
    }

    private void ReleaseOwner()
    {
        CharacterActor released = owner;
        if (movementRoutine != null && released != null) released.StopCoroutine(movementRoutine);
        movementRoutine = null;
        owner = null;
        IsEvacuating = false;
        usedAdministrationRoom = false;
        StatusText = string.Empty;
        if (released == null || released.IsDead) return;
        released.GetAbility<AbilityMove>()?.CancelActiveMovement();
        DefenseCombatPresentation.Ensure(released)?.SetStatus(string.Empty, false);
        released.SetAiPaused(false);
        released.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    private static int DistanceFromNearestDoor(Vector2Int cell, IReadOnlyList<Vector2Int> doors)
    {
        if (doors == null || doors.Count == 0) return 0;
        int minimum = int.MaxValue;
        for (int index = 0; index < doors.Count; index++)
            minimum = Mathf.Min(minimum, Manhattan(cell, doors[index]));
        return minimum;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
}
