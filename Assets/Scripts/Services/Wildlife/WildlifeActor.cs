using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WildlifeActor :
    MonoBehaviour,
    IGridOccupant,
    IInfoable,
    IWildlifeActorRestoreHost,
    ICapturedWildlifeFeedOutcomeTarget
{
    private Grid grid;
    private WildlifeSpeciesDefinition species;
    private WildlifeVisualPresentation visualPresentation;
    private WildlifeNaturalCondition naturalCondition;
    private Vector2Int gridPosition;
    private Queue<GridMoveStep> activePath = new Queue<GridMoveStep>();
    private Vector3 moveStartWorld;
    private Vector3 moveTargetWorld;
    private Vector2Int moveSourceGridPosition;
    private float moveProgress;
    private bool isMoving;
    private bool managedCaptiveMovement;
    private Vector2Int managedCaptiveTarget;
    private float nextPathRebuildAt;
    private int lastHorizontalDirection;
    private Vector2Int lastMoveTarget;
    private float headHealth;
    private float torsoHealth;
    private float limbHealth;
    private string lastCaptiveFeedCommitId = string.Empty;
    private IGridPathSearchBroker pathSearchBroker;
    private ICharacterAiWorldRegistry worldRegistry;
    private IGameClock gameClock;
    private IRandomStreamProvider randomStreamProvider;
    private IRandomStream randomStream;
    private IDoorAccessQuery doorAccessQuery;
    private WildlifeActorRestoreLifecycle restoreLifecycle;
    private float Now => gameClock != null ? gameClock.Time : 0f;
    private WildlifeVisualPresentation Visual =>
        visualPresentation ??= new WildlifeVisualPresentation(this);
    private WildlifeNaturalCondition NaturalCondition =>
        naturalCondition ??= new WildlifeNaturalCondition();
    private WildlifeActorRestoreLifecycle RestoreLifecycle =>
        restoreLifecycle ??= new WildlifeActorRestoreLifecycle(this);
    public string WildlifeId { get; private set; } = string.Empty;
    public string SpeciesId => species != null ? species.SpeciesId : string.Empty;
    public string DisplayName => species != null ? species.DisplayName : "야생동물";
    public string Description => species != null ? species.Description : string.Empty;
    public Sprite Sprite => species != null ? species.Sprite : null;
    public int MaxHealth => species != null ? species.MaxHealth : 1;
    public int CurrentHealth { get; private set; }
    public WildlifeState State { get; private set; } = WildlifeState.Idle;
    public Vector2Int GridPosition => gridPosition;
    public bool HuntDesignated { get; private set; }
    public bool PriorityHunt { get; private set; }
    public string ReservedByPersistentId { get; private set; } = string.Empty;
    public float Fear
    {
        get => NaturalCondition.Fear;
        private set => NaturalCondition.SetFear(value);
    }
    public float Hunger => NaturalCondition.Hunger;
    public float Thirst => NaturalCondition.Thirst;
    public WildlifeIntent Intent => NaturalCondition.Intent;
    public string IntentReason => NaturalCondition.IntentReason;
    public Vector2Int TerritoryCenter => NaturalCondition.TerritoryCenter;
    public Vector2Int HerdAnchorPosition => NaturalCondition.HerdAnchorPosition;
    public bool HasLastThreatPosition => NaturalCondition.HasLastThreatPosition;
    public Vector2Int LastThreatPosition => NaturalCondition.LastThreatPosition;
    public float LastThreatAge => NaturalCondition.GetLastThreatAge(Now);
    public float FearSensitivity => species != null ? species.FearSensitivity : 1f;
    public float Aggression => species != null ? species.Aggression : 0f;
    public int RetaliationDamage => species != null ? species.RetaliationDamage : 0;
    public bool CanEnterDungeon => species != null && species.CanEnterDungeon;
    public bool IsAlive => State != WildlifeState.Dead && CurrentHealth > 0;
    public bool IsDangerous => species != null && species.IsDangerous;
    public WildlifeSpeciesDefinition Species => species;
    public SpriteRenderer VisualRenderer => Visual.VisualRenderer;
    public bool IsMoving => isMoving;
    public int LastHorizontalDirection => lastHorizontalDirection;
    public Vector2Int LastMoveTarget => lastMoveTarget;
    public bool IsManagedCaptiveMovement => managedCaptiveMovement;
    public bool IsDetachedRestoreCandidate => RestoreLifecycle.IsDetached;
    public bool IsRestorePublicationPending =>
        RestoreLifecycle.IsPublicationPending;
    public float CombatMobility => Mathf.Lerp(0.45f, 1f, limbHealth / Mathf.Max(1f, GetLimbMaxHealth()));
    public string LastCaptiveFeedCommitId => lastCaptiveFeedCommitId;
    public void PrepareForDetachedRestore() => RestoreLifecycle.Prepare();
    public void PublishDetachedRestore() => RestoreLifecycle.Publish();
    public void ValidateDetachedRestorePublication() =>
        RestoreLifecycle.ValidatePublication();
    public void RollbackDetachedRestorePublication() =>
        RestoreLifecycle.RollbackPublication();
    public void CompleteDetachedRestorePublication() =>
        RestoreLifecycle.CompletePublication();
    public void DiscardDetachedRestore() => RestoreLifecycle.Discard();

    bool IWildlifeActorRestoreHost.IsInitialized =>
        grid != null || !string.IsNullOrWhiteSpace(WildlifeId);
    void IWildlifeActorRestoreHost.Register() => worldRegistry?.RegisterWildlife(this);
    void IWildlifeActorRestoreHost.Unregister() =>
        worldRegistry?.UnregisterWildlife(this);
    void IWildlifeActorRestoreHost.Discard()
    {
        PrepareForDespawn();
        DestroyImmediate(gameObject);
    }

#if UNITY_EDITOR
    public bool IsHealthBarVisibleForDebug => Visual.IsHealthBarVisibleForDebug;
#endif

    public int GridId => GetInstanceID();
    public bool IsGridDestroyed => this == null || State == WildlifeState.Dead;
    public bool IsGridVisitable => IsAlive;
    public bool IsGridMovement => false;

    public void ConfigureRuntimeServices(
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IDoorAccessQuery doorAccessQuery)
    {
        this.pathSearchBroker = pathSearchBroker
            ?? throw new System.ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new System.ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new System.ArgumentNullException(nameof(randomStreamProvider));
        this.doorAccessQuery = doorAccessQuery
            ?? throw new System.ArgumentNullException(nameof(doorAccessQuery));
    }

    public void Initialize(
        Grid runtimeGrid,
        WildlifeSpeciesDefinition definition,
        string wildlifeId,
        Vector2Int position,
        WildlifeSaveData saveData = null)
    {
        grid = runtimeGrid;
        species = definition;
        WildlifeId = wildlifeId ?? string.Empty;
        randomStream = (randomStreamProvider
            ?? throw new System.InvalidOperationException(
                "Wildlife runtime services must be configured before initialization."))
            .Get(RandomStreamScopeIds.WildlifeActor(
                string.IsNullOrWhiteSpace(WildlifeId)
                    ? throw new System.InvalidOperationException(
                        "Wildlife requires a persistent ID before random-stream binding.")
                    : WildlifeId));
        CurrentHealth = saveData != null ? Mathf.Clamp(saveData.health, 0, MaxHealth) : MaxHealth;
        headHealth = saveData != null && saveData.hasCombatBodyProfile
            ? Mathf.Clamp(saveData.headHealth, 0f, GetHeadMaxHealth())
            : GetHeadMaxHealth();
        torsoHealth = saveData != null && saveData.hasCombatBodyProfile
            ? Mathf.Clamp(saveData.torsoHealth, 0f, GetTorsoMaxHealth())
            : GetTorsoMaxHealth();
        limbHealth = saveData != null && saveData.hasCombatBodyProfile
            ? Mathf.Clamp(saveData.limbHealth, 0f, GetLimbMaxHealth())
            : GetLimbMaxHealth();
        State = saveData != null ? saveData.state : WildlifeState.Idle;
        HuntDesignated = saveData != null && saveData.huntDesignated;
        PriorityHunt = saveData != null && saveData.priorityHunt;
        ReservedByPersistentId = saveData?.reservedByPersistentId ?? string.Empty;
        lastCaptiveFeedCommitId =
            saveData?.lastCaptiveFeedCommitId ?? string.Empty;
        NaturalCondition.Initialize(
            saveData,
            position,
            Now,
            saveData == null ? NextRange(0.15f, 0.45f) : 0f,
            saveData == null ? NextRange(0.1f, 0.35f) : 0f);
        lastMoveTarget = position;
        nextPathRebuildAt = Now + NextRange(0.4f, 1.8f);
        Visual.EnsureVisual();
        RegisterAt(position);
        if (!IsDetachedRestoreCandidate)
        {
            worldRegistry?.RegisterWildlife(this);
        }
    }

    public void Tick(float deltaTime)
    {
        if (!IsAlive)
        {
            return;
        }

        if (State == WildlifeState.Captured)
        {
            if (managedCaptiveMovement && isMoving)
            {
                TickMovement(deltaTime);
            }

            if (managedCaptiveMovement
                && !isMoving
                && gridPosition == managedCaptiveTarget)
            {
                managedCaptiveMovement = false;
            }

            Visual.UpdateMarker();
            Visual.UpdateHealthBar();
            return;
        }

        if (isMoving)
        {
            TickMovement(deltaTime);
        }

        TickNaturalState(deltaTime);
        Visual.UpdateMarker();
        Visual.UpdateHealthBar();
    }

    public bool CanRepath(float now)
    {
        return !isMoving && now >= nextPathRebuildAt;
    }

    public bool TrySetPath(Vector2Int targetPosition, float now)
    {
        if (grid == null || !IsAlive || isMoving)
        {
            return false;
        }

        if (targetPosition == gridPosition)
        {
            ScheduleArrivalDwell(now);
            return false;
        }

        Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
            grid,
            gridPosition,
            targetPosition,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForWildlife(WildlifeId));
        if (path == null || path.Count == 0)
        {
            nextPathRebuildAt = now + NextRange(0.5f, 1.5f);
            return false;
        }

        activePath = path;
        lastMoveTarget = targetPosition;
        nextPathRebuildAt = now + NextRange(0.5f, 1.5f);
        StartNextMoveStep();
        return true;
    }

    public bool TrySetManagedCaptivePath(Vector2Int targetPosition, float now)
    {
        if (grid == null
            || !IsAlive
            || State != WildlifeState.Captured
            || isMoving)
        {
            return false;
        }

        if (targetPosition == gridPosition)
        {
            managedCaptiveTarget = targetPosition;
            managedCaptiveMovement = false;
            return true;
        }

        Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
            grid,
            gridPosition,
            targetPosition,
            GridPathSearchPriority.Urgent,
            GridTraversalContext.ForWildlife(WildlifeId));
        if (path == null || path.Count == 0)
        {
            return false;
        }

        activePath = path;
        lastMoveTarget = targetPosition;
        managedCaptiveTarget = targetPosition;
        managedCaptiveMovement = true;
        nextPathRebuildAt = now + 0.5f;
        StartNextMoveStep();
        return true;
    }

    public void SetHuntDesignation(bool designated, bool priority)
    {
        HuntDesignated = designated;
        PriorityHunt = designated && priority;
        if (designated && State != WildlifeState.Dead)
        {
            State = WildlifeState.Hunted;
            nextPathRebuildAt = Now;
        }
        else if (!designated && State == WildlifeState.Hunted)
        {
            State = WildlifeState.Idle;
        }

        Visual.UpdateMarker();
    }

    public bool TryReserve(CharacterActor actor)
    {
        if (actor == null || !IsAlive || !HuntDesignated)
        {
            return false;
        }

        string actorId = actor.Identity != null ? actor.Identity.PersistentId : string.Empty;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            actorId = actor.name;
        }

        if (!string.IsNullOrWhiteSpace(ReservedByPersistentId)
            && ReservedByPersistentId != actorId)
        {
            return false;
        }

        ReservedByPersistentId = actorId;
        State = WildlifeState.Hunted;
        return true;
    }

    public void ReleaseReservation(CharacterActor actor)
    {
        string actorId = actor != null && actor.Identity != null
            ? actor.Identity.PersistentId
            : string.Empty;
        if (string.IsNullOrWhiteSpace(actorId) || ReservedByPersistentId == actorId)
        {
            ReservedByPersistentId = string.Empty;
        }
    }

    public int ApplyDamage(int damage, CharacterActor hunter)
    {
        int applied = Mathf.Clamp(damage, 0, CurrentHealth);
        CurrentHealth -= applied;
        NaturalCondition.AddFear(Mathf.Max(1f, applied) * FearSensitivity);
        nextPathRebuildAt = Now;
        if (hunter != null)
        {
            RegisterThreat(hunter.GetNowXY(), Mathf.Max(0.2f, applied / Mathf.Max(1f, MaxHealth)));
        }

        if (CurrentHealth <= 0)
        {
            State = WildlifeState.Dead;
            worldRegistry?.UnregisterWildlife(this);
            Unregister();
        }
        else if (Aggression > 0.65f && hunter != null)
        {
            State = WildlifeState.Retaliating;
        }
        else
        {
            State = WildlifeState.Fleeing;
        }

        Visual.UpdateMarker();
        Visual.UpdateHealthBar(force: true);
        return applied;
    }

    public int ApplyCombatDamage(CombatAttackResult result, CharacterActor hunter)
    {
        if (!result.Executed || !result.Hit || result.AppliedDamage <= 0f || !IsAlive)
        {
            return 0;
        }

        float partDamage = result.AppliedDamage;
        switch (result.BodyPart)
        {
            case CombatBodyPart.Head:
                headHealth = Mathf.Max(0f, headHealth - partDamage);
                break;
            case CombatBodyPart.Torso:
                torsoHealth = Mathf.Max(0f, torsoHealth - partDamage);
                break;
            default:
                limbHealth = Mathf.Max(0f, limbHealth - partDamage);
                break;
        }

        int applied = ApplyDamage(Mathf.Max(1, Mathf.RoundToInt(partDamage)), hunter);
        if (IsAlive && (headHealth <= 0f || torsoHealth <= 0f))
        {
            applied += ApplyDamage(CurrentHealth, hunter);
        }

        return applied;
    }

    public int DebugHeal(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return 0;
        }

        int applied = Mathf.Clamp(amount, 0, MaxHealth - CurrentHealth);
        CurrentHealth += applied;
        Visual.UpdateHealthBar(force: true);
        return applied;
    }

    public void ChangeLayer(string layer)
    {
        Visual.ChangeLayer(layer);
    }

    public void SetPredatorStalking()
    {
        if (IsAlive)
        {
            State = WildlifeState.PredatorStalking;
            Visual.UpdateMarker();
        }
    }

    public void SetGrazing()
    {
        if (IsAlive && State != WildlifeState.Hunted && State != WildlifeState.Fleeing)
        {
            State = WildlifeState.Grazing;
            Visual.UpdateMarker();
        }
    }

    public void SetIdle()
    {
        if (IsAlive && State != WildlifeState.Hunted)
        {
            State = WildlifeState.Idle;
            Visual.UpdateMarker();
        }
    }

    public void MarkLeaving()
    {
        if (IsAlive)
        {
            State = WildlifeState.Leaving;
            Visual.UpdateMarker();
        }
    }

    public void RegisterThreat(Vector2Int position, float intensity)
    {
        NaturalCondition.RegisterThreat(position, intensity, FearSensitivity, Now);
        nextPathRebuildAt = Now;
    }

    public void SetHerdAnchor(Vector2Int position)
    {
        NaturalCondition.SetHerdAnchor(position);
    }

    public void SetTerritoryCenter(Vector2Int position)
    {
        NaturalCondition.SetTerritoryCenter(position);
    }

    public void WarpTo(Vector2Int position)
    {
        activePath.Clear();
        isMoving = false;
        lastMoveTarget = position;
        nextPathRebuildAt = Now + NextRange(0.6f, 1.8f);
        Visual.RestorePose();
        RegisterAt(position);
    }

    internal bool TryRebindGridAfterExpansion(
        Grid expectedCurrent,
        Grid replacement,
        out string failureReason)
    {
        if (!CanRebindGridAfterExpansion(
                expectedCurrent,
                replacement,
                out failureReason))
        {
            return false;
        }

        activePath.Clear();
        isMoving = false;
        managedCaptiveMovement = false;
        Visual.RestorePose();
        grid = replacement;
        Vector3 world = grid.GetWorldPos(gridPosition);
        transform.position = new Vector3(world.x, world.y, transform.position.z);
        Visual.RefreshSortingForGridPosition();
        return true;
    }

#if UNITY_EDITOR
    public bool TryRebindGridAfterExpansionForDebug(
        Grid expectedCurrent,
        Grid replacement,
        out string failureReason) =>
        TryRebindGridAfterExpansion(expectedCurrent, replacement, out failureReason);
#endif

    internal bool CanRebindGridAfterExpansion(
        Grid expectedCurrent,
        Grid replacement,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (ReferenceEquals(grid, replacement)
            && ReferenceEquals(
                replacement?.GetGridCell(gridPosition)
                    ?.GetOccupant(GridLayer.Wildlife),
                this))
        {
            // Restore participants roll back in reverse order. The wildlife
            // participant can therefore restore an actor to the prior grid
            // before the facility/grid participant publishes that same prior
            // grid. Treat that publication as an idempotent rebind instead of
            // rejecting the whole rollback because the actor is already home.
            return true;
        }
        if (!ReferenceEquals(grid, expectedCurrent))
        {
            failureReason =
                $"Wildlife '{WildlifeId}' is not bound to the grid being expanded.";
            return false;
        }
        if (replacement == null
            || !ReferenceEquals(
                replacement.GetGridCell(gridPosition)
                    ?.GetOccupant(GridLayer.Wildlife),
                this))
        {
            failureReason =
                $"Expanded grid did not preserve wildlife '{WildlifeId}' at {gridPosition}.";
            return false;
        }

        return true;
    }

    public void BeginManagedCarry(Transform carrier)
    {
        if (carrier == null || grid == null)
        {
            return;
        }

        activePath.Clear();
        isMoving = false;
        managedCaptiveMovement = false;
        Visual.RestorePose();
        grid.RemoveOccupant(
            this,
            GridLayer.Wildlife,
            new[] { gridPosition },
            disconnectPositions: false);
        SetCaptured(true);
        transform.SetParent(carrier, worldPositionStays: false);
        transform.localPosition = new Vector3(0.28f, 0.18f, 0f);
    }

    public void EndManagedCarry(Vector2Int position, Transform parent)
    {
        transform.SetParent(parent, worldPositionStays: true);
        WarpTo(position);
        SetCaptured(true);
    }

    public bool TryEndManagedCarry(
        Vector2Int position,
        Transform expectedCarrier,
        Transform parent,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (grid == null)
        {
            failureReason = "Wildlife grid authority is unavailable.";
            return false;
        }
        if (expectedCarrier == null || transform.parent != expectedCarrier)
        {
            failureReason = "Wildlife managed-carry ownership does not match the expected carrier.";
            return false;
        }

        GridCell destination = grid.GetGridCell(position);
        IGridOccupant occupant = destination?.GetOccupant(GridLayer.Wildlife);
        if (destination == null
            || !grid.IsWalkable(position)
            || (occupant != null && !ReferenceEquals(occupant, this)))
        {
            failureReason = $"Wildlife delivery cell {position} is unavailable.";
            return false;
        }

        Vector2Int carriedPosition = gridPosition;
        transform.SetParent(parent, worldPositionStays: true);
        if (!TryRegisterAt(position))
        {
            gridPosition = carriedPosition;
            transform.SetParent(expectedCarrier, worldPositionStays: false);
            transform.localPosition = new Vector3(0.28f, 0.18f, 0f);
            failureReason = $"Wildlife delivery registration failed at {position}.";
            return false;
        }

        SetCaptured(true);
        if (gridPosition != position
            || !ReferenceEquals(
                grid.GetGridCell(position)?.GetOccupant(GridLayer.Wildlife),
                this))
        {
            Unregister();
            gridPosition = carriedPosition;
            transform.SetParent(expectedCarrier, worldPositionStays: false);
            transform.localPosition = new Vector3(0.28f, 0.18f, 0f);
            failureReason = $"Wildlife delivery authority did not converge at {position}.";
            return false;
        }

        return true;
    }

    public void SetCaptured(bool captured)
    {
        activePath.Clear();
        isMoving = false;
        managedCaptiveMovement = false;
        Visual.RestorePose();
        if (captured)
        {
            State = WildlifeState.Captured;
            HuntDesignated = false;
            PriorityHunt = false;
            ReservedByPersistentId = string.Empty;
            SetIntent(WildlifeIntent.Rest, "우리 안에서 대기");
        }
        else if (IsAlive)
        {
            State = WildlifeState.Idle;
            SetIntent(WildlifeIntent.ReturnToTerritory, "방생되어 영역으로 복귀");
        }
        Visual.UpdateMarker();
    }

    public void AdvanceCaptiveNeeds(
        float deltaTime,
        float hungerPerSecond,
        float thirstPerSecond)
    {
        if (!IsAlive || State != WildlifeState.Captured || deltaTime <= 0f)
        {
            return;
        }

        NaturalCondition.AdvanceNeeds(
            deltaTime,
            hungerPerSecond,
            thirstPerSecond);
    }

    public void SatisfyCaptiveNeeds(float food, float water)
    {
        NaturalCondition.SatisfyNeeds(food, water);
    }

    public bool TryApplyCaptiveFeedOutcome(
        string commitId,
        float hungerTarget,
        int healthTarget,
        out bool applied)
    {
        applied = false;
        string commit = commitId ?? string.Empty;
        if (commit.Length == 0
            || !string.Equals(commit, commit.Trim(), System.StringComparison.Ordinal)
            || float.IsNaN(hungerTarget)
            || float.IsInfinity(hungerTarget)
            || hungerTarget is < 0f or > 1f
            || healthTarget < 0
            || healthTarget > MaxHealth)
        {
            return false;
        }
        if (string.Equals(
                lastCaptiveFeedCommitId,
                commit,
                System.StringComparison.Ordinal))
        {
            return true;
        }

        NaturalCondition.SetHunger(hungerTarget);
        if (CurrentHealth > healthTarget)
        {
            ApplyDamage(CurrentHealth - healthTarget, null);
        }
        lastCaptiveFeedCommitId = commit;
        applied = true;
        return true;
    }

    public void SetIntent(WildlifeIntent newIntent, string reason)
    {
        NaturalCondition.SetIntent(newIntent, reason);
    }

    public void ChangeHunger(float delta)
    {
        NaturalCondition.ChangeHunger(delta);
    }

    public void ChangeThirst(float delta)
    {
        NaturalCondition.ChangeThirst(delta);
    }

    public WildlifeSaveData Capture()
    {
        WildlifeSaveData saveData = new WildlifeSaveData
        {
            wildlifeId = WildlifeId,
            speciesId = SpeciesId,
            health = CurrentHealth,
            state = State,
            gridX = gridPosition.x,
            gridY = gridPosition.y,
            huntDesignated = HuntDesignated,
            priorityHunt = PriorityHunt,
            reservedByPersistentId = ReservedByPersistentId,
            hasCombatBodyProfile = true,
            headHealth = headHealth,
            torsoHealth = torsoHealth,
            limbHealth = limbHealth,
            lastCaptiveFeedCommitId = lastCaptiveFeedCommitId
        };
        NaturalCondition.CaptureInto(saveData);
        return saveData;
    }

    private float GetHeadMaxHealth()
    {
        return Mathf.Max(4f, MaxHealth * 0.3f);
    }

    private float GetTorsoMaxHealth()
    {
        return Mathf.Max(6f, MaxHealth * 0.65f);
    }

    private float GetLimbMaxHealth()
    {
        return Mathf.Max(5f, MaxHealth * 0.45f);
    }

    private void OnDestroy()
    {
        worldRegistry?.UnregisterWildlife(this);
        Unregister();
    }

    public void PrepareForDespawn()
    {
        isMoving = false;
        activePath?.Clear();
        Visual.RestorePose();
        worldRegistry?.UnregisterWildlife(this);
        Unregister();
        grid = null;
    }

    private void TickNaturalState(float deltaTime)
    {
        float foodNeed = species != null ? species.DailyFoodNeed : 1f;
        float waterNeed = species != null ? species.DailyWaterNeed : 1f;
        NaturalCondition.Tick(deltaTime, foodNeed, waterNeed, Now);
    }

    private void RegisterAt(Vector2Int position)
    {
        TryRegisterAt(position);
    }

    private bool TryRegisterAt(Vector2Int position)
    {
        Unregister();
        gridPosition = position;
        if (grid == null
            || !grid.RegisterOccupant(
                this,
                GridLayer.Wildlife,
                new[] { gridPosition },
                connectPositions: false))
        {
            return false;
        }

        Vector3 world = grid.GetWorldPos(gridPosition);
        transform.position = new Vector3(world.x, world.y, transform.position.z);
        Visual.RefreshSortingForGridPosition();
        return true;
    }

    private void Unregister()
    {
        if (grid == null)
        {
            return;
        }

        grid.RemoveOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, disconnectPositions: false);
    }

    private void StartNextMoveStep()
    {
        if (activePath == null || activePath.Count == 0)
        {
            isMoving = false;
            ScheduleArrivalDwell(Now);
            return;
        }

        GridMoveStep step = activePath.Dequeue();
        if (grid == null || !CanMoveTo(step.To))
        {
            isMoving = false;
            activePath.Clear();
            Visual.RestorePose();
            nextPathRebuildAt = Now + NextRange(0.8f, 1.8f);
            return;
        }

        Vector3 fromWorld = grid.GetWorldPos(step.From);
        Vector3 target = grid.GetWorldPos(step.To);
        int horizontalDirection = Mathf.RoundToInt(Mathf.Sign(target.x - fromWorld.x));
        if (horizontalDirection != 0)
        {
            lastHorizontalDirection = horizontalDirection;
            Visual.SetHorizontalDirection(horizontalDirection);
        }

        moveSourceGridPosition = gridPosition;
        moveStartWorld = transform.position;
        moveTargetWorld = new Vector3(target.x, target.y, transform.position.z);
        grid.RemoveOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, disconnectPositions: false);
        gridPosition = step.To;
        grid.RegisterOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, connectPositions: false);
        Visual.RefreshSortingForGridPosition();
        moveProgress = 0f;
        isMoving = true;
    }

    private bool CanMoveTo(Vector2Int target)
    {
        if (grid == null || !grid.IsValidGridPos(target) || !grid.IsWalkable(target))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(target);
        if (cell == null || !cell.CanOccupy(GridLayer.Wildlife))
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        if (doorAccessQuery != null
            && !doorAccessQuery.CanTraverse(
                grid,
                target,
                GridTraversalContext.ForWildlife(WildlifeId),
                out _))
        {
            return false;
        }

        return managedCaptiveMovement
            || CanEnterDungeon
            || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    private void TickMovement(float deltaTime)
    {
        if (doorAccessQuery != null
            && !doorAccessQuery.CanTraverse(
                grid,
                gridPosition,
                GridTraversalContext.ForWildlife(WildlifeId),
                out _))
        {
            grid.RemoveOccupant(
                this,
                GridLayer.Wildlife,
                new[] { gridPosition },
                disconnectPositions: false);
            gridPosition = moveSourceGridPosition;
            grid.RegisterOccupant(
                this,
                GridLayer.Wildlife,
                new[] { gridPosition },
                connectPositions: false);
            transform.position = moveStartWorld;
            isMoving = false;
            activePath.Clear();
            Visual.RestorePose();
            Visual.RefreshSortingForGridPosition();
            nextPathRebuildAt = Now + NextRange(0.8f, 1.8f);
            return;
        }

        float speed = species != null ? species.MoveSpeed : 1f;
        float duration = Mathf.Max(0.12f, 0.45f / Mathf.Max(0.1f, speed));
        moveProgress += deltaTime / duration;
        float normalized = Mathf.Clamp01(moveProgress);
        float eased = normalized * normalized * (3f - 2f * normalized);
        transform.position = Vector3.Lerp(moveStartWorld, moveTargetWorld, eased);
        Visual.ApplyMovementBob(normalized);
        if (moveProgress < 1f)
        {
            return;
        }

        transform.position = moveTargetWorld;
        isMoving = false;
        Visual.RestorePose();
        if (activePath.Count > 0)
        {
            StartNextMoveStep();
            return;
        }

        ScheduleArrivalDwell(Now);
    }

    private void ScheduleArrivalDwell(float now)
    {
        float duration = Intent switch
        {
            WildlifeIntent.Flee => NextRange(0.08f, 0.25f),
            WildlifeIntent.HuntPrey => NextRange(0.1f, 0.35f),
            WildlifeIntent.LeaveMap => NextRange(0.1f, 0.3f),
            WildlifeIntent.Drink => NextRange(1.6f, 3.2f),
            WildlifeIntent.Forage => NextRange(1.8f, 3.8f),
            WildlifeIntent.Rest => NextRange(3.2f, 6.5f),
            _ => NextRange(1.2f, 3.4f)
        };
        float restPreference = species != null ? species.RestPreference : 0.5f;
        nextPathRebuildAt = now + duration * Mathf.Lerp(0.85f, 1.25f, restPreference);
    }

    private float NextRange(float minInclusive, float maxInclusive)
    {
        IRandomStream stream = randomStream
            ?? throw new System.InvalidOperationException(
                "Wildlife random stream is unavailable before initialization.");
        return Mathf.Lerp(minInclusive, maxInclusive, stream.NextFloat());
    }

}
