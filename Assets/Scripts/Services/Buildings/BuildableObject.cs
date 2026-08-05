using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class BuildableObject : MonoBehaviour,
    IGridOccupant,
    IGridMovementOccupant,
    IGridBuildingOccupantCapability,
    IBuildingWorldEntryPort
{
    private const float DefaultAiReservationSeconds = 12f;
    private readonly List<Vector2Int> mutableBuildPoses = new List<Vector2Int>();
    private IReadOnlyList<Vector2Int> buildPosesView;
    public int id { get; private set; }
    public Vector2Int centerPos { get; protected set; }
    public IReadOnlyList<Vector2Int> buildPoses =>
        buildPosesView ??= ReadOnlyView.List(mutableBuildPoses);
    public BuildingSO BuildingData { get; private set; }

    protected Grid grid;
    public BuildingCategory category { get; private set; }

    public event Action OnBuildingDestroyed;
    public event Action<BuildableObject> OnBuildingClicked;
    public bool isDestroy;
    [SerializeField] private bool isDamaged;
    [SerializeField] private int facilityLevel = 1;
    [SerializeField] private string persistentInstanceId = string.Empty;
    [SerializeField] private FacilityRuntimeState facilityState = new FacilityRuntimeState();
    private BuildingOccupancy occupancy;
    private BuildingAssignment assignment;
    private BuildableObjectStateAndCapabilityController stateAndCapabilities;
    private BuildableObjectSpatialQuery spatialQuery;
    private IBuildingResearchWorkPort blueprintResearchWorkService;
    private IBuildingFacilityStateChangePort facilityCandidateCache;
    private IBuildingRoomPolicyPort roomFacilityPolicy;
    private IBuildingEquipmentCraftingRuntimePort combatEquipmentRuntime;
    private IBuildingWorldRegistryPort worldRegistry;
    private IBuildingItemStackPort worldItemStackRuntime;
    private IBuildingAbilityRuntimeDispatcher abilityRuntimeDispatcher;
    private IBuildingPaidFacilityContractPort paidFacilityContracts;
    private IBuildingCoverDurabilityPort coverDurabilityRegistry;
    private IBuildingEvolutionStatePort evolutionState;
    private IBuildingPresentationSettingsPort userSettings;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IBuildingVisitEventPort visitEvents;
    private IBuildingInfoPresentationPort infoPresentation;
    private IBuildingDamageRulePort debugRules;
    private bool registeredWithWorldRegistry;
    private bool detachedRestoreCandidate;
    private bool synchronizedWithPaidContracts;

    public int GridId => id;
    public bool IsGridDestroyed => isDestroy;
    public bool IsGridVisitable => isVisitable();
    public bool IsGridMovement => category == BuildingCategory.Movement;
    public virtual GridMoveType GridMoveType => IsGridMovement ? GridMoveType.Instant : GridMoveType.Walk;
    public bool BlocksGridMovement
    {
        get
        {
            if (isDestroy)
            {
                return false;
            }

            bool isDoor = this is Door || BuildingData?.IsDoor == true;
            bool isStructuralWall = BuildingData != null
                ? BuildingData.IsStructuralWall
                : category == BuildingCategory.Wall;
            return isStructuralWall && !isDoor;
        }
    }
    public bool AllowsInteriorWalkability =>
        !isDestroy && Facility?.IsVisitorFacility == true;
    public Grid Grid => grid;
    public FacilityData Facility => BuildingData != null ? BuildingData.Facility : null;
    public bool IsDamaged => isDamaged;
    public int FacilityLevel => facilityLevel;
    public bool IsDetachedRestoreCandidate => detachedRestoreCandidate;
    public BuildingInstanceId PersistentInstanceId =>
        (BuildingInstanceId)persistentInstanceId;
    public BuildingInstanceId BuildingInstanceId => PersistentInstanceId;
    public bool IsBuildingDestroyed => isDestroy;

    public BuildingInstanceId RequirePersistentInstanceId()
    {
        BuildingInstanceId value = PersistentInstanceId;
        return value.IsValid
            ? value
            : throw new InvalidOperationException(
                $"Building '{name}' has no persistent BuildingInstanceId.");
    }
    private BuildingOccupancy Occupancy =>
        occupancy ??= new BuildingOccupancy(this);
    private BuildingAssignment Assignment =>
        assignment ??= new BuildingAssignment(this);
    private BuildableObjectStateAndCapabilityController StateAndCapabilities =>
        stateAndCapabilities ??= new BuildableObjectStateAndCapabilityController(
            this,
            MarkFacilityDynamicStateDirty);
    private BuildableObjectSpatialQuery SpatialQuery =>
        spatialQuery ??= new BuildableObjectSpatialQuery(transform, this);
    public int CurrentUserCount => Occupancy.CurrentUserCount;
    public FacilityRuntimeState FacilityState => facilityState ??= new FacilityRuntimeState();
    internal IBuildingWorldRegistryPort WorldRegistry => worldRegistry;
    internal IBuildingItemStackPort WorldItemStackRuntime => worldItemStackRuntime;
    internal IBuildingAbilityRuntimeDispatcher AbilityRuntimeDispatcher =>
        abilityRuntimeDispatcher;
    public int EffectiveCapacity => ResolveRoomFacilityPolicy().GetEffectiveCapacity(this);

    public int ActiveVisitReservationCount =>
        Occupancy.ActiveVisitReservationCount;
    public IBuildingCharacterPort WorkerReservation => Assignment.WorkerReservation;

    public virtual void Start()
    {
    }

    [Inject]
    public void ConstructPersistentIdentity(IPersistentIdGenerator persistentIds)
    {
        if (PersistentInstanceId.IsValid)
        {
            return;
        }

        persistentInstanceId = (persistentIds
            ?? throw new ArgumentNullException(nameof(persistentIds)))
            .NewBuildingInstanceId()
            .Value;
    }

    public void RestorePersistentIdentity(BuildingInstanceId value)
    {
        if (!value.IsValid)
        {
            throw new ArgumentException(
                "A valid BuildingInstanceId is required.",
                nameof(value));
        }

        persistentInstanceId = value.Value;
    }

    protected virtual void OnDestroy()
    {
        RemovePaidFacilityContractIfNeeded();
        UnregisterFromWorldRegistry();
        DetachFromGridIfStillRegistered();
    }

    public void PrepareForDetachedRestore()
    {
        if (BuildingData != null || registeredWithWorldRegistry)
        {
            throw new InvalidOperationException(
                "Detached restore mode must be selected before building initialization.");
        }

        detachedRestoreCandidate = true;
    }

    public void PublishDetachedRestore()
    {
        if (!detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only a detached restore candidate can be published.");
        }

        detachedRestoreCandidate = false;
        RegisterWithWorldRegistryIfReady();
        SynchronizePaidFacilityContractIfReady();
    }

    public void DiscardDetachedRestore()
    {
        if (!detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only a detached restore candidate can be discarded.");
        }

        isDestroy = true;
        DetachFromGridIfStillRegistered();
        grid = null;
        DestroyImmediate(gameObject);
    }

    [Inject]
    public void ConstructBuildableObject(
        IBuildingResearchWorkPort blueprintResearchWorkService,
        IBuildingFacilityStateChangePort facilityCandidateCache,
        IBuildingRoomPolicyPort roomFacilityPolicy,
        IBuildingEquipmentCraftingRuntimePort combatEquipmentRuntime,
        IBuildingWorldRegistryPort worldRegistry,
        IBuildingItemStackPort worldItemStackRuntime,
        IBuildingAbilityRuntimeDispatcher abilityRuntimeDispatcher,
        IGameClock gameClock,
        IBuildingPaidFacilityContractPort paidFacilityContracts,
        IBuildingEvolutionStatePort evolutionState)
    {
        this.blueprintResearchWorkService = blueprintResearchWorkService
            ?? throw new ArgumentNullException(nameof(blueprintResearchWorkService));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.roomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
        this.worldRegistry = worldRegistry;
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.worldItemStackRuntime = worldItemStackRuntime;
        this.abilityRuntimeDispatcher = abilityRuntimeDispatcher;
        this.gameClock = gameClock;
        this.paidFacilityContracts = paidFacilityContracts;
        this.evolutionState = evolutionState
            ?? throw new ArgumentNullException(nameof(evolutionState));
        RegisterWithWorldRegistryIfReady();
        SynchronizePaidFacilityContractIfReady();
    }

    [Inject]
    public void ConstructBuildableObjectEventBus(
        IGameEventBus gameEventBus,
        IBuildingVisitEventPort visitEvents,
        IBuildingInfoPresentationPort infoPresentation)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.visitEvents = visitEvents
            ?? throw new ArgumentNullException(nameof(visitEvents));
        this.infoPresentation = infoPresentation
            ?? throw new ArgumentNullException(nameof(infoPresentation));
    }

    [Inject]
    public void ConstructDebugRules(IBuildingDamageRulePort debugRules)
    {
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
    }

    [Inject]
    public void ConstructCoverDurabilityRegistry(
        IBuildingCoverDurabilityPort coverDurabilityRegistry,
        IBuildingPresentationSettingsPort userSettings)
    {
        this.coverDurabilityRegistry = coverDurabilityRegistry
            ?? throw new ArgumentNullException(nameof(coverDurabilityRegistry));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
    }

    internal IBuildingCoverDurabilityPort RequireCoverDurabilityRegistry()
    {
        return coverDurabilityRegistry
            ?? throw new InvalidOperationException(
                $"{nameof(BuildableObject)} requires {nameof(IBuildingCoverDurabilityPort)} injection.");
    }

    internal bool ReducedMotion => userSettings?.ReducedMotion == true;

    public virtual void SetGrid(Grid grid)
    {
        this.grid = grid;
    }

    public void SetRuntimeGridPosition(Vector2Int position)
    {
        centerPos = position;
        mutableBuildPoses.Clear();
        if (BuildingData != null)
        {
            mutableBuildPoses.AddRange(
                BuildingData.GetGridPosList(position));
        }

        if (grid != null)
        {
            Vector3 worldPosition = grid.GetWorldPos(position);
            if (BuildingData != null
                && BuildingData.Placement.HasEvenWidth)
            {
                worldPosition.x += 0.5f;
            }

            transform.position = worldPosition;
        }

        MarkFacilityDynamicStateDirty();
    }

    public virtual void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        if (buildingSO == null)
        {
            throw new ArgumentNullException(nameof(buildingSO));
        }

        buildingSO.ValidateAbilitiesOrThrow();
        BuildingData = buildingSO;
        GridBuildingPlacement placement = buildingSO.Placement;
        id = buildingSO.id;
        isDestroy = false;
        isDamaged = false;
        facilityLevel = 1;
        Occupancy.Reset();
        Assignment.Reset();
        facilityState ??= new FacilityRuntimeState();
        facilityState.CopyFrom(null);
        StateAndCapabilities.ResetStateModules();
        RegisterStateModule(new FacilityRuntimeStateModule(this));
        (evolutionState ?? throw new InvalidOperationException(
                $"{nameof(BuildableObject)} requires {nameof(IBuildingEvolutionStatePort)} injection."))
            .EnsureInitialized(this);
        foreach (BuildingAbility ability in buildingSO.Abilities)
        {
            if (ability is IBuildingRuntimeStateAbility stateAbility)
            {
                RegisterStateModule(stateAbility.CreateStateModule(this));
            }
        }
        if (buildingSO.GetAbility<BuildingStructuralIntegrityAbility>() == null
            && BuildingStructuralIntegrityDefaults.TryCreate(
                buildingSO,
                out BuildingStructuralIntegrityAbility structuralAbility))
        {
            RegisterStateModule(
                BuildingStructuralIntegrity.Ensure(this, structuralAbility));
        }
        centerPos = buildPos;
        category = placement.Category;
        mutableBuildPoses.Clear();
        mutableBuildPoses.AddRange(placement.GetGridPosList(buildPos));
        ModularFacilityRuntimeEffects.ConfigureVisual(this);
        RegisterWithWorldRegistryIfReady();
        SynchronizePaidFacilityContractIfReady();
    }

    public virtual Vector3 GetMovementWorldPosition(Vector2Int gridPosition)
    {
        if (grid == null)
        {
            return transform.position;
        }

        Vector3 anchor = grid.GetWorldPos(gridPosition);
        if (BuildingData != null)
        {
            anchor += (Vector3)BuildingData.movementAnchorOffset;
        }

        return anchor;
    }

    public bool TryGetFacilityOccupiedWorldPosition(Vector3 fromWorld, out Vector3 worldPosition)
    {
        worldPosition = fromWorld;
        if (grid == null || buildPoses == null || buildPoses.Count == 0)
        {
            return false;
        }

        if (TryFindNearestFacilityCell(fromWorld, requireRegisteredOccupant: true, out Vector2Int facilityCell)
            || TryFindNearestFacilityCell(fromWorld, requireRegisteredOccupant: false, out facilityCell))
        {
            worldPosition = grid.GetWorldPos(facilityCell);
            return true;
        }

        return false;
    }

    public bool ContainsGridPosition(Vector2Int gridPosition)
    {
        return buildPoses != null && buildPoses.Contains(gridPosition);
    }

    public Vector3 GetFacilityAnchorWorldPosition(string purposeId, Vector3 fromWorld)
    {
        return TryGetFacilityAnchorWorldPosition(purposeId, fromWorld, out Vector3 worldPosition)
            ? worldPosition
            : transform.position;
    }

    public bool TryGetFacilityAnchorWorldPosition(
        string purposeId,
        Vector3 fromWorld,
        out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        if (grid == null || buildPoses == null || buildPoses.Count == 0)
        {
            return false;
        }

        if (TryGetConfiguredFacilityAnchorWorldPosition(purposeId, fromWorld, out worldPosition))
        {
            return true;
        }

        if (FacilityAnchorPurposeCatalog.TryGet(purposeId, out FacilityAnchorPurposeDefinition definition))
        {
            return definition.FallbackResolver(this, fromWorld, out worldPosition);
        }

        return TryGetFacilityOccupiedWorldPosition(fromWorld, out worldPosition)
            || TryGetHorizontalFootprintAnchorWorldPosition(0.5f, out worldPosition);
    }

    private bool TryFindNearestFacilityCell(
        Vector3 fromWorld,
        bool requireRegisteredOccupant,
        out Vector2Int result)
    {
        return SpatialQuery.TryFindNearestFacilityCell(
            grid,
            buildPoses,
            centerPos,
            fromWorld,
            requireRegisteredOccupant,
            out result);
    }

    private bool TryGetConfiguredFacilityAnchorWorldPosition(
        string purposeId,
        Vector3 fromWorld,
        out Vector3 worldPosition)
    {
        return SpatialQuery.TryGetConfiguredFacilityAnchorWorldPosition(
            grid,
            BuildingData,
            centerPos,
            purposeId,
            fromWorld,
            out worldPosition);
    }

    public bool TryGetHorizontalFootprintAnchorWorldPosition(
        float normalizedX,
        out Vector3 worldPosition)
    {
        return SpatialQuery.TryGetHorizontalFootprintAnchorWorldPosition(
            grid,
            buildPoses,
            centerPos,
            normalizedX,
            out worldPosition);
    }

    public float GetWorkUrgency(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid
            && WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition definition)
            ? GetLegacyWorkUrgency(
                FacilityWorkTypeMap.GetRequired(definition))
            : 0f;
    }

    internal virtual float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        return Assignment.GetLegacyWorkUrgency(workType);
    }

    public virtual bool isVisitable()
    {
        return CanVisit((IBuildingCharacterPort)null, out _);
    }

    public void TriggerWorldInfoClick()
    {
        if (OnBuildingClicked != null)
        {
            OnBuildingClicked.Invoke(this);
            return;
        }

        (infoPresentation
            ?? throw new InvalidOperationException(
                $"{nameof(BuildableObject)} requires {nameof(IBuildingInfoPresentationPort)} injection."))
            .ShowBuildingInfo(this);
    }

    public void DestroySelf()
    {
        RemovePaidFacilityContractIfNeeded();
        OnBuildingDestroyed?.Invoke();
        isDestroy = true;
        UnregisterFromWorldRegistry();
        DetachFromGridIfStillRegistered();
        MarkFacilityDynamicStateDirty();
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    internal void RetireForWorldReplacement()
    {
        RemovePaidFacilityContractIfNeeded();
        isDestroy = true;
        UnregisterFromWorldRegistry();
        DetachFromGridIfStillRegistered();
        MarkFacilityDynamicStateDirty();
        DestroyImmediate(gameObject);
    }

    private void RemovePaidFacilityContractIfNeeded()
    {
        if (!synchronizedWithPaidContracts)
        {
            return;
        }

        paidFacilityContracts?.RemoveFacility(this);
        synchronizedWithPaidContracts = false;
    }

    private void SynchronizePaidFacilityContractIfReady()
    {
        if (synchronizedWithPaidContracts
            || detachedRestoreCandidate
            || paidFacilityContracts == null
            || BuildingData == null
            || isDestroy)
        {
            return;
        }

        paidFacilityContracts.SynchronizeFacility(this);
        synchronizedWithPaidContracts = true;
    }

    private void RegisterWithWorldRegistryIfReady()
    {
        if (registeredWithWorldRegistry
            || detachedRestoreCandidate
            || worldRegistry == null
            || BuildingData == null
            || isDestroy)
        {
            return;
        }

        worldRegistry.RegisterBuilding(this);
        registeredWithWorldRegistry = true;
    }

    private void UnregisterFromWorldRegistry()
    {
        if (!registeredWithWorldRegistry)
        {
            return;
        }

        worldRegistry?.UnregisterBuilding(this);
        registeredWithWorldRegistry = false;
    }

    private void DetachFromGridIfStillRegistered()
    {
        if (grid == null || BuildingData == null || buildPoses == null || buildPoses.Count == 0)
        {
            return;
        }

        GridBuildingPlacement placement = BuildingData.Placement;
        GridLayer registeredLayer = placement.Layer;
        bool disconnectPositions = placement.IsMovement;
        for (int i = 0; i < buildPoses.Count; i++)
        {
            GridCell cell = grid.GetGridCell(buildPoses[i]);
            if (ReferenceEquals(cell?.GetOccupant(GridLayer.Construction), this))
            {
                registeredLayer = GridLayer.Construction;
                disconnectPositions = false;
                break;
            }
        }

        grid.RemoveOccupant(this, registeredLayer, buildPoses, disconnectPositions);
    }

    protected void MarkFacilityDynamicStateDirty() =>
        ResolveFacilityCandidateCache().MarkDynamicStateDirty();

    public void NotifyStructuralStateChanged() =>
        MarkFacilityDynamicStateDirty();

    public BuildingRoomOperationalSnapshot GetRoomOperationalProfile() =>
        ResolveRoomFacilityPolicy().GetOperationalProfile(this);

    public void RestoreFacilityState(FacilityRuntimeState state) =>
        StateAndCapabilities.RestoreFacilityState(FacilityState, state);

    public void RecordCompletedWorkCycle() =>
        StateAndCapabilities.RecordCompletedWorkCycle(FacilityState);

    public void SetCleanliness(float value) =>
        StateAndCapabilities.SetCleanliness(FacilityState, value);

    public IReadOnlyList<IBuildingStateModule> GetStateModules() =>
        StateAndCapabilities.GetStateModules();

    protected void RegisterStateModule(IBuildingStateModule module) =>
        StateAndCapabilities.RegisterStateModule(module);

    public bool TryGetStateModule<TModule>(
        string moduleId,
        out TModule module)
        where TModule : class, IBuildingStateModule
        => StateAndCapabilities.TryGetStateModule(moduleId, out module);

    public TModule RequireStateModule<TModule>(string moduleId)
        where TModule : class, IBuildingStateModule
        => StateAndCapabilities.RequireStateModule<TModule>(moduleId);

    private IBuildingFacilityStateChangePort ResolveFacilityCandidateCache()
        => StateAndCapabilities.RequireDependency(facilityCandidateCache);

    internal IBuildingRoomPolicyPort ResolveRoomFacilityPolicy()
        => StateAndCapabilities.RequireDependency(roomFacilityPolicy);

    internal IBuildingResearchWorkPort ResolveBlueprintResearchWorkService()
        => StateAndCapabilities.RequireDependency(blueprintResearchWorkService);

    public bool TryGetCombatEquipmentRuntime(
        out IBuildingEquipmentCraftingRuntimePort runtime)
    {
        return (runtime = combatEquipmentRuntime) != null;
    }

    public bool HasPendingEquipmentCraftWork() =>
        BuildableObjectStateAndCapabilityController.HasPendingEquipmentCraftWork(
            BuildingData,
            combatEquipmentRuntime);

    public void SetDamaged(bool value)
    {
        if ((debugRules ?? throw new InvalidOperationException(
                $"{nameof(BuildableObject)} requires {nameof(IBuildingDamageRulePort)} injection."))
            .ShouldBlockFacilityDamage(value))
        {
            return;
        }

        if (isDamaged == value)
        {
            return;
        }

        isDamaged = value;
        MarkFacilityDynamicStateDirty();
    }

    public void SetFacilityLevel(int value)
    {
        int nextLevel = Mathf.Max(1, value);
        if (facilityLevel == nextLevel)
        {
            return;
        }

        facilityLevel = nextLevel;
        MarkFacilityDynamicStateDirty();
    }

    public bool SupportsFacilityRole(FacilityRole role) =>
        Facility != null && Facility.SupportsRole(role);

    public bool SupportsWork(WorkTypeId workTypeId) =>
        Facility != null && Facility.SupportsWork(workTypeId);

    internal bool SupportsWork(FacilityWorkType workType) =>
        Facility != null && Facility.SupportsWork(workType);

    public bool CanVisit(IBuildingCharacterPort visitor, out string failureReason) =>
        Occupancy.CanVisit(visitor, out failureReason);

    public bool TryBeginUse(IBuildingCharacterPort visitor, out string failureReason) =>
        Occupancy.TryBeginUse(visitor, out failureReason);

    protected void PublishGameEvent<TEvent>(TEvent gameEvent)
    {
        GameEventBus.Publish(gameEvent);
    }

    protected IGameEventBus GameEventBus =>
        gameEventBus
        ?? throw new InvalidOperationException(
            $"{GetType().Name} requires {nameof(IGameEventBus)} injection.");

    public void EndUse(IBuildingCharacterPort visitor) => Occupancy.EndUse(visitor);

    public bool TryReserveVisit(
        IBuildingCharacterPort visitor,
        out string failureReason,
        float seconds = DefaultAiReservationSeconds)
    {
        return Occupancy.TryReserveVisit(visitor, out failureReason, seconds);
    }

    public void RefreshVisitReservation(
        IBuildingCharacterPort visitor,
        float seconds = DefaultAiReservationSeconds)
    {
        Occupancy.RefreshVisitReservation(visitor, seconds);
    }

    public void ReleaseVisitReservation(IBuildingCharacterPort visitor) =>
        Occupancy.ReleaseVisitReservation(visitor);

    public bool TryReserveWorker(
        IBuildingCharacterPort worker,
        out FacilityAssignmentStatus status,
        float seconds = DefaultAiReservationSeconds)
    {
        return Assignment.TryReserveWorker(worker, out status, seconds);
    }

    public void RefreshWorkerReservation(
        IBuildingCharacterPort worker,
        float seconds = DefaultAiReservationSeconds)
    {
        Assignment.RefreshWorkerReservation(worker, seconds);
    }

    public bool HasWorkerReservationForOther(IBuildingCharacterPort worker) =>
        Assignment.HasWorkerReservationForOther(worker);

    public void ReleaseWorkerReservation(IBuildingCharacterPort worker) =>
        Assignment.ReleaseWorkerReservation(worker);

    public bool CanAssignWork(
        WorkTypeId workTypeId,
        out string failureReason)
    {
        FacilityAssignmentStatus status =
            Assignment.GetWorkAssignmentStatus(workTypeId);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    internal bool CanAssignWork(
        FacilityWorkType workType,
        out string failureReason)
    {
        FacilityAssignmentStatus status =
            Assignment.GetWorkAssignmentStatus(workType);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public FacilityAssignmentStatus GetWorkAssignmentStatus(
        WorkTypeId workTypeId) => Assignment.GetWorkAssignmentStatus(workTypeId);

    internal FacilityAssignmentStatus GetWorkAssignmentStatus(
        FacilityWorkType workType) => Assignment.GetWorkAssignmentStatus(workType);

    internal IBuildingPaidFacilityContractPort PaidFacilityContracts =>
        paidFacilityContracts;
    internal float OccupancyAndAssignmentTime => GameTime;

    internal void NotifyOccupancyOrAssignmentChanged()
    {
        MarkFacilityDynamicStateDirty();
    }

    internal void RecordFacilityUse(IBuildingCharacterPort visitor)
    {
        FacilityState.completedUses++;
        FacilityState.cleanliness = Mathf.Clamp(
            FacilityState.cleanliness - 1.5f,
            0f,
            100f);
        MarkFacilityDynamicStateDirty();
        (visitEvents ?? throw new InvalidOperationException(
                $"{nameof(BuildableObject)} requires {nameof(IBuildingVisitEventPort)} injection."))
            .PublishVisit(visitor, this);
    }

    protected IGameClock GameClock => gameClock
        ?? throw new InvalidOperationException(
            $"{nameof(BuildableObject)} requires {nameof(IGameClock)} injection.");
    public bool HasInjectedGameClock => gameClock != null;
    protected float GameTime => GameClock.Time;
    protected float GameDeltaTime => GameClock.DeltaTime;
    protected int GameFrameCount => GameClock.FrameCount;

}
