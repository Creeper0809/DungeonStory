using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WildlifeActor : MonoBehaviour, IGridOccupant, IInfoable
{
    private const string VisualRootName = "WildlifeVisual";
    private const string HealthRootName = "WildlifeHealth";
    private const string DefaultSortingLayerName = "Default";
    private const int DefaultSortingOrder = 120;
    private const int MarkerSortingOrderOffset = 36;
    private const int HealthSortingOrderOffset = 32;
    private const float HealthBarWidth = 0.72f;
    private const float HealthBarHeight = 0.045f;
    private const float MovementBobHeight = 0.035f;

    private static Sprite fallbackSprite;
    private static Sprite markerSprite;
    private static Material sharedLineMaterial;

    private Grid grid;
    private WildlifeSpeciesDefinition species;
    private Transform visualRoot;
    private SpriteRenderer visualRenderer;
    private SpriteRenderer markerRenderer;
    private Transform healthRoot;
    private LineRenderer healthBackgroundLine;
    private LineRenderer healthFillLine;
    private Vector2Int gridPosition;
    private Queue<GridMoveStep> activePath = new Queue<GridMoveStep>();
    private Vector3 moveStartWorld;
    private Vector3 moveTargetWorld;
    private Vector2Int moveSourceGridPosition;
    private Vector3 visualRootRestLocalPosition;
    private float moveProgress;
    private bool isMoving;
    private bool managedCaptiveMovement;
    private Vector2Int managedCaptiveTarget;
    private float nextPathRebuildAt;
    private int lastHorizontalDirection;
    private Vector2Int lastMoveTarget;
    private float hunger;
    private float thirst;
    private WildlifeIntent intent = WildlifeIntent.Wander;
    private string intentReason = string.Empty;
    private Vector2Int territoryCenter;
    private Vector2Int herdAnchorPosition;
    private Vector2Int lastThreatPosition;
    private bool hasLastThreatPosition;
    private float lastThreatTime;
    private float headHealth;
    private float torsoHealth;
    private float limbHealth;
    private string currentSortingLayerName = DefaultSortingLayerName;
    private int currentSortingOrder = DefaultSortingOrder;
    private IGridPathSearchBroker pathSearchBroker;
    private ICharacterAiWorldRegistry worldRegistry;
    private IGameClock gameClock = new UnityGameClock();
    private IRandomStreamProvider randomStreamProvider;
    private IRandomStream randomStream;
    private IDoorAccessQuery doorAccessQuery;

    private float Now => gameClock != null ? gameClock.Time : 0f;

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
    public float Fear { get; private set; }
    public float Hunger => hunger;
    public float Thirst => thirst;
    public WildlifeIntent Intent => intent;
    public string IntentReason => intentReason;
    public Vector2Int TerritoryCenter => territoryCenter;
    public Vector2Int HerdAnchorPosition => herdAnchorPosition;
    public bool HasLastThreatPosition => hasLastThreatPosition;
    public Vector2Int LastThreatPosition => lastThreatPosition;
    public float LastThreatAge => hasLastThreatPosition
        ? Mathf.Max(0f, Now - lastThreatTime)
        : float.MaxValue;
    public float FearSensitivity => species != null ? species.FearSensitivity : 1f;
    public float Aggression => species != null ? species.Aggression : 0f;
    public int RetaliationDamage => species != null ? species.RetaliationDamage : 0;
    public bool CanEnterDungeon => species != null && species.CanEnterDungeon;
    public bool IsAlive => State != WildlifeState.Dead && CurrentHealth > 0;
    public bool IsDangerous => species != null && species.IsDangerous;
    public WildlifeSpeciesDefinition Species => species;
    public SpriteRenderer VisualRenderer => visualRenderer;
    public bool IsMoving => isMoving;
    public int LastHorizontalDirection => lastHorizontalDirection;
    public Vector2Int LastMoveTarget => lastMoveTarget;
    public bool IsManagedCaptiveMovement => managedCaptiveMovement;
    public float CombatMobility => Mathf.Lerp(0.45f, 1f, limbHealth / Mathf.Max(1f, GetLimbMaxHealth()));

#if UNITY_EDITOR
    public bool IsHealthBarVisibleForDebug => healthRoot != null && healthRoot.gameObject.activeSelf;
#endif

    public int GridId => GetInstanceID();
    public bool IsGridDestroyed => this == null || State == WildlifeState.Dead;
    public bool IsGridVisitable => IsAlive;
    public bool IsGridMovement => false;

    public void ConfigureRuntimeServices(
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock = null,
        IRandomStreamProvider randomStreamProvider = null,
        IDoorAccessQuery doorAccessQuery = null)
    {
        this.pathSearchBroker = pathSearchBroker
            ?? throw new System.ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new System.ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? this.gameClock;
        this.randomStreamProvider = randomStreamProvider;
        this.doorAccessQuery = doorAccessQuery;
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
        randomStream = (randomStreamProvider ?? new RandomStreamProvider(1))
            .Get("wildlife.actor." + (string.IsNullOrWhiteSpace(WildlifeId) ? "unknown" : WildlifeId));
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
        Fear = saveData != null ? Mathf.Max(0f, saveData.fear) : 0f;
        hunger = saveData != null ? Mathf.Clamp01(saveData.hunger) : NextRange(0.15f, 0.45f);
        thirst = saveData != null ? Mathf.Clamp01(saveData.thirst) : NextRange(0.1f, 0.35f);
        intent = saveData != null ? saveData.intent : WildlifeIntent.Wander;
        intentReason = saveData?.intentReason ?? string.Empty;
        territoryCenter = saveData != null && saveData.hasTerritory
            ? new Vector2Int(saveData.territoryX, saveData.territoryY)
            : position;
        herdAnchorPosition = saveData != null && saveData.hasHerdAnchor
            ? new Vector2Int(saveData.herdAnchorX, saveData.herdAnchorY)
            : territoryCenter;
        hasLastThreatPosition = saveData != null && saveData.hasLastThreat;
        lastThreatPosition = hasLastThreatPosition
            ? new Vector2Int(saveData.lastThreatX, saveData.lastThreatY)
            : position;
        lastThreatTime = hasLastThreatPosition ? Now : 0f;
        lastMoveTarget = position;
        nextPathRebuildAt = Now + NextRange(0.4f, 1.8f);
        EnsureVisual();
        RegisterAt(position);
        worldRegistry?.RegisterWildlife(this);
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

            UpdateMarker();
            UpdateHealthBar();
            return;
        }

        if (isMoving)
        {
            TickMovement(deltaTime);
        }

        TickNaturalState(deltaTime);
        UpdateMarker();
        UpdateHealthBar();
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
            GridTraversalContext.ForWildlife(this));
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
            GridTraversalContext.ForWildlife(this));
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

        UpdateMarker();
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
        Fear += Mathf.Max(1f, applied) * FearSensitivity;
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

        UpdateMarker();
        UpdateHealthBar(force: true);
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
        UpdateHealthBar(force: true);
        return applied;
    }

    public void ChangeLayer(string layer)
    {
        currentSortingLayerName = string.IsNullOrWhiteSpace(layer)
            ? DefaultSortingLayerName
            : layer;
        ApplyVisualSorting();
    }

    public void SetPredatorStalking()
    {
        if (IsAlive)
        {
            State = WildlifeState.PredatorStalking;
            UpdateMarker();
        }
    }

    public void SetGrazing()
    {
        if (IsAlive && State != WildlifeState.Hunted && State != WildlifeState.Fleeing)
        {
            State = WildlifeState.Grazing;
            UpdateMarker();
        }
    }

    public void SetIdle()
    {
        if (IsAlive && State != WildlifeState.Hunted)
        {
            State = WildlifeState.Idle;
            UpdateMarker();
        }
    }

    public void MarkLeaving()
    {
        if (IsAlive)
        {
            State = WildlifeState.Leaving;
            UpdateMarker();
        }
    }

    public void RegisterThreat(Vector2Int position, float intensity)
    {
        hasLastThreatPosition = true;
        lastThreatPosition = position;
        lastThreatTime = Now;
        Fear = Mathf.Clamp(Fear + Mathf.Max(0.1f, intensity) * FearSensitivity, 0f, 12f);
        nextPathRebuildAt = Now;
    }

    public void SetHerdAnchor(Vector2Int position)
    {
        herdAnchorPosition = position;
    }

    public void SetTerritoryCenter(Vector2Int position)
    {
        territoryCenter = position;
    }

    public void WarpTo(Vector2Int position)
    {
        activePath.Clear();
        isMoving = false;
        lastMoveTarget = position;
        nextPathRebuildAt = Now + NextRange(0.6f, 1.8f);
        RestoreVisualRootPose();
        RegisterAt(position);
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
        RestoreVisualRootPose();
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

    public void SetCaptured(bool captured)
    {
        activePath.Clear();
        isMoving = false;
        managedCaptiveMovement = false;
        RestoreVisualRootPose();
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
        UpdateMarker();
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

        hunger = Mathf.Clamp01(
            hunger + Mathf.Max(0f, hungerPerSecond) * deltaTime);
        thirst = Mathf.Clamp01(
            thirst + Mathf.Max(0f, thirstPerSecond) * deltaTime);
    }

    public void SatisfyCaptiveNeeds(float food, float water)
    {
        hunger = Mathf.Clamp01(hunger - Mathf.Max(0f, food));
        thirst = Mathf.Clamp01(thirst - Mathf.Max(0f, water));
    }

    public void SetIntent(WildlifeIntent newIntent, string reason)
    {
        intent = newIntent;
        intentReason = reason ?? string.Empty;
    }

    public void ChangeHunger(float delta)
    {
        hunger = Mathf.Clamp01(hunger + delta);
    }

    public void ChangeThirst(float delta)
    {
        thirst = Mathf.Clamp01(thirst + delta);
    }

    public WildlifeSaveData Capture()
    {
        return new WildlifeSaveData
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
            fear = Fear,
            hunger = hunger,
            thirst = thirst,
            intent = intent,
            intentReason = intentReason,
            hasTerritory = true,
            territoryX = territoryCenter.x,
            territoryY = territoryCenter.y,
            hasHerdAnchor = true,
            herdAnchorX = herdAnchorPosition.x,
            herdAnchorY = herdAnchorPosition.y,
            hasLastThreat = hasLastThreatPosition,
            lastThreatX = lastThreatPosition.x,
            lastThreatY = lastThreatPosition.y,
            hasCombatBodyProfile = true,
            headHealth = headHealth,
            torsoHealth = torsoHealth,
            limbHealth = limbHealth
        };
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
        RestoreVisualRootPose();
        worldRegistry?.UnregisterWildlife(this);
        Unregister();
        grid = null;
    }

    private void TickNaturalState(float deltaTime)
    {
        float foodNeed = species != null ? species.DailyFoodNeed : 1f;
        float waterNeed = species != null ? species.DailyWaterNeed : 1f;
        hunger = Mathf.Clamp01(hunger + deltaTime * foodNeed / 300f);
        thirst = Mathf.Clamp01(thirst + deltaTime * waterNeed / 240f);
        Fear = Mathf.Max(0f, Fear - deltaTime * 0.08f);
        if (hasLastThreatPosition && Now - lastThreatTime > 90f)
        {
            hasLastThreatPosition = false;
        }
    }

    private void EnsureVisual()
    {
        visualRoot = EnsureVisualRoot();
        visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null && rootRenderer != visualRenderer)
        {
            if (visualRenderer.sprite == null)
            {
                CopySpriteRenderer(rootRenderer, visualRenderer);
            }

            RemoveRootSpriteRenderer(rootRenderer);
        }

        visualRenderer.sprite = Sprite != null ? Sprite : GetFallbackSprite();
        visualRenderer.color = ResolveFallbackColor();
        ApplyVisualFootAnchor();
        ApplyVisualSorting();
        EnsureMarker();
        EnsureHealthBar();

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        ConfigureCollider(collider);
        gameObject.name = "Wildlife_" + DisplayName + "_" + WildlifeId;
        UpdateAttachedVisualPositions();
        UpdateMarker();
        UpdateHealthBar(force: true);
    }

    private Transform EnsureVisualRoot()
    {
        Transform root = transform.Find(VisualRootName);
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new GameObject(VisualRootName);
        root = rootObject.transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        return root;
    }

    private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
        target.sharedMaterials = source.sharedMaterials;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.maskInteraction = source.maskInteraction;
        target.drawMode = SpriteDrawMode.Simple;
        target.size = source.sprite != null ? (Vector2)source.sprite.bounds.size : Vector2.one;
    }

    private static void RemoveRootSpriteRenderer(SpriteRenderer rootRenderer)
    {
        if (rootRenderer == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(rootRenderer);
            return;
        }

        DestroyImmediate(rootRenderer);
    }

    private void ApplyVisualFootAnchor()
    {
        if (visualRoot == null || visualRenderer == null || visualRenderer.sprite == null)
        {
            return;
        }

        Bounds bounds = visualRenderer.sprite.bounds;
        visualRootRestLocalPosition = new Vector3(
            -bounds.center.x,
            -bounds.min.y,
            0f);
        visualRoot.localPosition = visualRootRestLocalPosition;
    }

    private void ApplyVisualSorting()
    {
        if (visualRenderer != null)
        {
            visualRenderer.sortingLayerName = currentSortingLayerName;
            visualRenderer.sortingOrder = currentSortingOrder;
        }

        if (markerRenderer != null)
        {
            markerRenderer.sortingLayerName = currentSortingLayerName;
            markerRenderer.sortingOrder = currentSortingOrder + MarkerSortingOrderOffset;
        }

        ApplyLineSorting(healthBackgroundLine, currentSortingLayerName, currentSortingOrder + HealthSortingOrderOffset);
        ApplyLineSorting(healthFillLine, currentSortingLayerName, currentSortingOrder + HealthSortingOrderOffset + 1);
    }

    private void RefreshSortingForGridPosition()
    {
        currentSortingOrder = DefaultSortingOrder + Mathf.Clamp(gridPosition.y, 0, 20) * 2;
        ApplyVisualSorting();
    }

    private void ConfigureCollider(BoxCollider2D collider)
    {
        Bounds bounds = visualRenderer != null && visualRenderer.sprite != null
            ? visualRenderer.sprite.bounds
            : new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
        float width = Mathf.Clamp(bounds.size.x * 0.72f, 0.45f, 1.25f);
        float height = Mathf.Clamp(bounds.size.y * 0.72f, 0.35f, 1.05f);
        collider.size = new Vector2(width, height);
        collider.offset = new Vector2(0f, height * 0.5f);
    }

    private Color ResolveFallbackColor()
    {
        if (Sprite != null)
        {
            return Color.white;
        }

        return IsDangerous
            ? new Color(0.55f, 0.18f, 0.2f, 1f)
            : new Color(0.55f, 0.72f, 0.48f, 1f);
    }

    private void EnsureMarker()
    {
        if (markerRenderer != null)
        {
            return;
        }

        GameObject marker = new GameObject("WildlifeStateMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = new Vector3(0.48f, 0.48f, 1f);
        markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = GetMarkerSprite();
        markerRenderer.enabled = false;
        ApplyVisualSorting();
    }

    private void EnsureHealthBar()
    {
        if (healthRoot == null)
        {
            Transform existingRoot = transform.Find(HealthRootName);
            healthRoot = existingRoot != null
                ? existingRoot
                : new GameObject(HealthRootName).transform;
            healthRoot.SetParent(transform, false);
            healthRoot.localRotation = Quaternion.identity;
            healthRoot.localScale = Vector3.one;
        }

        healthBackgroundLine = EnsureHealthLine("HealthBackground", new Color(0.02f, 0.04f, 0.05f, 0.82f));
        healthFillLine = EnsureHealthLine("HealthFill", new Color(0.32f, 0.84f, 0.58f, 1f));
        ApplyVisualSorting();
    }

    private LineRenderer EnsureHealthLine(string objectName, Color color)
    {
        Transform child = healthRoot.Find(objectName);
        if (child == null)
        {
            child = new GameObject(objectName).transform;
            child.SetParent(healthRoot, false);
        }

        if (!child.TryGetComponent(out LineRenderer line))
        {
            line = child.gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.widthMultiplier = HealthBarHeight;
        Material material = ResolveLineMaterial();
        if (material != null)
        {
            line.sharedMaterial = material;
        }

        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void UpdateAttachedVisualPositions()
    {
        float top = GetVisualTopLocalY();
        if (markerRenderer != null)
        {
            markerRenderer.transform.localPosition = new Vector3(0f, top + 0.18f, -0.01f);
        }

        if (healthRoot != null)
        {
            healthRoot.localPosition = new Vector3(0f, top + 0.06f, -0.01f);
        }
    }

    private float GetVisualTopLocalY()
    {
        if (visualRoot == null || visualRenderer == null || visualRenderer.sprite == null)
        {
            return 0.9f;
        }

        return visualRoot.localPosition.y + visualRenderer.sprite.bounds.max.y;
    }

    private void UpdateMarker()
    {
        if (markerRenderer == null)
        {
            return;
        }

        Color markerColor;
        bool visible = true;
        if (!IsAlive)
        {
            visible = false;
            markerColor = Color.clear;
        }
        else if (HuntDesignated)
        {
            markerColor = PriorityHunt
                ? new Color(1f, 0.12f, 0.08f, 1f)
                : new Color(0.82f, 0.16f, 0.16f, 1f);
        }
        else if (State == WildlifeState.Fleeing || State == WildlifeState.Leaving)
        {
            markerColor = new Color(1f, 0.83f, 0.18f, 1f);
        }
        else if (IsDangerous || State == WildlifeState.PredatorStalking || State == WildlifeState.Retaliating)
        {
            markerColor = new Color(0.72f, 0.05f, 0.09f, 1f);
        }
        else
        {
            visible = false;
            markerColor = Color.clear;
        }

        markerRenderer.enabled = visible;
        markerRenderer.color = markerColor;
    }

    private void UpdateHealthBar(bool force = false)
    {
        if (healthRoot == null || healthBackgroundLine == null || healthFillLine == null)
        {
            return;
        }

        bool visible = IsAlive && CurrentHealth < MaxHealth;
        if (healthRoot.gameObject.activeSelf != visible)
        {
            healthRoot.gameObject.SetActive(visible);
        }

        if (!visible && !force)
        {
            return;
        }

        float health01 = Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, MaxHealth));
        SetLineSpan(healthBackgroundLine, -HealthBarWidth * 0.5f, HealthBarWidth * 0.5f);
        float fillRight = Mathf.Lerp(-HealthBarWidth * 0.5f, HealthBarWidth * 0.5f, health01);
        SetLineSpan(healthFillLine, -HealthBarWidth * 0.5f, fillRight);

        Color color = health01 > 0.6f
            ? new Color(0.32f, 0.84f, 0.58f, 1f)
            : health01 > 0.3f
                ? new Color(0.95f, 0.74f, 0.22f, 1f)
                : new Color(0.93f, 0.22f, 0.18f, 1f);
        healthFillLine.startColor = color;
        healthFillLine.endColor = color;
    }

    private static void SetLineSpan(LineRenderer line, float left, float right)
    {
        if (line == null)
        {
            return;
        }

        line.SetPosition(0, new Vector3(left, 0f, 0f));
        line.SetPosition(1, new Vector3(right, 0f, 0f));
    }

    private static void ApplyLineSorting(LineRenderer line, string layerName, int order)
    {
        if (line == null)
        {
            return;
        }

        line.sortingLayerName = string.IsNullOrWhiteSpace(layerName)
            ? DefaultSortingLayerName
            : layerName;
        line.sortingOrder = order;
    }

    private static Material ResolveLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        sharedLineMaterial = new Material(shader)
        {
            name = "WildlifeHealthLineMaterial"
        };
        return sharedLineMaterial;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool body = x >= 3 && x <= 12 && y >= 4 && y <= 10;
                bool head = x >= 10 && x <= 14 && y >= 7 && y <= 12;
                bool leg = (x == 5 || x == 10) && y >= 1 && y <= 4;
                texture.SetPixel(x, y, body || head || leg ? Color.white : clear);
            }
        }

        texture.Apply();
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0f),
            16f);
        fallbackSprite.name = "WildlifeFallbackSprite";
        return fallbackSprite;
    }

    private static Sprite GetMarkerSprite()
    {
        if (markerSprite != null)
        {
            return markerSprite;
        }

        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool diamond = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) <= 3.5f;
                bool outline = Mathf.Abs(x - 3.5f) + Mathf.Abs(y - 3.5f) >= 2.5f;
                texture.SetPixel(x, y, diamond ? (outline ? Color.black : Color.white) : clear);
            }
        }

        texture.Apply();
        markerSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            8f);
        markerSprite.name = "WildlifeStateMarkerSprite";
        return markerSprite;
    }

    private void RegisterAt(Vector2Int position)
    {
        Unregister();
        gridPosition = position;
        if (grid != null)
        {
            grid.RegisterOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, connectPositions: false);
            Vector3 world = grid.GetWorldPos(gridPosition);
            transform.position = new Vector3(world.x, world.y, transform.position.z);
            RefreshSortingForGridPosition();
        }
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
            RestoreVisualRootPose();
            nextPathRebuildAt = Now + NextRange(0.8f, 1.8f);
            return;
        }

        Vector3 fromWorld = grid.GetWorldPos(step.From);
        Vector3 target = grid.GetWorldPos(step.To);
        int horizontalDirection = Mathf.RoundToInt(Mathf.Sign(target.x - fromWorld.x));
        if (horizontalDirection != 0)
        {
            lastHorizontalDirection = horizontalDirection;
            if (visualRenderer != null)
            {
                visualRenderer.flipX = horizontalDirection < 0;
            }
        }

        moveSourceGridPosition = gridPosition;
        moveStartWorld = transform.position;
        moveTargetWorld = new Vector3(target.x, target.y, transform.position.z);
        grid.RemoveOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, disconnectPositions: false);
        gridPosition = step.To;
        grid.RegisterOccupant(this, GridLayer.Wildlife, new[] { gridPosition }, connectPositions: false);
        RefreshSortingForGridPosition();
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
                GridTraversalContext.ForWildlife(this),
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
                GridTraversalContext.ForWildlife(this),
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
            RestoreVisualRootPose();
            RefreshSortingForGridPosition();
            nextPathRebuildAt = Now + NextRange(0.8f, 1.8f);
            return;
        }

        float speed = species != null ? species.MoveSpeed : 1f;
        float duration = Mathf.Max(0.12f, 0.45f / Mathf.Max(0.1f, speed));
        moveProgress += deltaTime / duration;
        float normalized = Mathf.Clamp01(moveProgress);
        float eased = normalized * normalized * (3f - 2f * normalized);
        transform.position = Vector3.Lerp(moveStartWorld, moveTargetWorld, eased);
        if (visualRoot != null)
        {
            float bob = Mathf.Sin(normalized * Mathf.PI) * MovementBobHeight;
            visualRoot.localPosition = visualRootRestLocalPosition + Vector3.up * bob;
        }
        if (moveProgress < 1f)
        {
            return;
        }

        transform.position = moveTargetWorld;
        isMoving = false;
        RestoreVisualRootPose();
        if (activePath.Count > 0)
        {
            StartNextMoveStep();
            return;
        }

        ScheduleArrivalDwell(Now);
    }

    private void ScheduleArrivalDwell(float now)
    {
        float duration = intent switch
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
            ?? (randomStreamProvider ?? new RandomStreamProvider(1))
                .Get("wildlife.actor.fallback");
        return Mathf.Lerp(minInclusive, maxInclusive, stream.NextFloat());
    }

    private void RestoreVisualRootPose()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualRootRestLocalPosition;
        }
    }
}
