using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public enum CharacterSpawnRejection
{
    None = 0,
    OwnerUnavailable,
    PoolUnavailable,
    DefinitionMissing,
    RecruitmentLocked,
    RegularCustomerIneligible,
    PopulationProfileNotCreated,
    PopulationProfilePreparing,
    PopulationProfileAlreadyVisiting,
    PopulationProfileUnavailable,
    EntryUnavailable,
    PrefabInvalid
}

public class CharacterSpawner : BuildableObject,IInteractable
{
    private const float FallbackOutsideSpawnDistance = 1f;
    public CharacterSO[] characters;
    public GameObject characterPrefab;
    [SerializeField] private Transform outsideSpawnPoint;
    [SerializeField] private Transform entryDoorPoint;
    [SerializeField] private Vector2Int entryGridPosition = new Vector2Int(4, 0);

    private readonly CharacterRespawnSchedule respawnSchedule = new();
    private readonly CharacterSpawnerSceneApplicationAdapter sceneAdapter = new();
    public IObjectPool<GameObject> characterPool;
    private WaitForSeconds spawnDelay = new WaitForSeconds(0.3f);
    private RegularCustomerRuntime regularCustomers;
    private IGridSystemProvider gridSystemProvider;
    private IRunVariableRuntimeReader runVariableReader;
    private ICharacterSpawnObjectFactory characterObjectFactory;
    private IRunCharacterCatalog characterCatalog;
    private ICharacterPopulationService characterPopulationService;
    private IFactionContractQuery factionContracts;
    private IOwnerRunManagerProvider ownerRunManagerProvider;
    private IBuildingWorldQuery buildingWorldQuery;
    private IRandomStream respawnRandomStream;
    private bool scopeTeardownStarted;
    private bool deterministicSimulationPausedForDiagnostics;
    private long visitorExitHandoffAttemptCount;
    private long visitorExitHandoffCompletedCount;
    private string lastVisitorExitPersistentId = string.Empty;
    private string lastVisitorExitHandoffStage = string.Empty;

    public long VisitorExitHandoffAttemptCount => visitorExitHandoffAttemptCount;
    public long VisitorExitHandoffCompletedCount => visitorExitHandoffCompletedCount;
    public string LastVisitorExitPersistentId => lastVisitorExitPersistentId;
    public string LastVisitorExitHandoffStage => lastVisitorExitHandoffStage;

    [Inject]
    public void Construct(
        RegularCustomerRuntime regularCustomers,
        IGridSystemProvider gridSystemProvider,
        IRunVariableRuntimeReader runVariableReader,
        ICharacterSpawnObjectFactory characterObjectFactory,
        IRunCharacterCatalog characterCatalog,
        ICharacterPopulationService characterPopulationService,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        IBuildingWorldQuery buildingWorldQuery,
        IRandomStreamProvider randomStreamProvider,
        IFactionContractQuery factionContracts)
    {
        this.regularCustomers = regularCustomers
            ?? throw new ArgumentNullException(nameof(regularCustomers));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.characterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        this.characterPopulationService = characterPopulationService
            ?? throw new ArgumentNullException(nameof(characterPopulationService));
        this.ownerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        this.buildingWorldQuery = buildingWorldQuery
            ?? throw new ArgumentNullException(nameof(buildingWorldQuery));
        this.factionContracts = factionContracts
            ?? throw new ArgumentNullException(nameof(factionContracts));
        respawnRandomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-spawner");
        scopeTeardownStarted = false;
        sceneAdapter.ResetInjectedProjection();
    }

    private void Awake()
    {
        EnsureRuntimeState();
    }

    public override void Start()
    {
        base.Start();
        centerPos = GetEntryGridPosition();
        EnsureRuntimeState();

        if (sceneAdapter.BeginSpawnRoutine())
        {
            StartCoroutine(StartSpawn());
        }
    }

    private void EnsureRuntimeState()
    {
        if (sceneAdapter.IsRuntimeInitialized)
        {
            EnsureCharacterPool();
            return;
        }

        if (characters == null)
        {
            characters = new CharacterSO[0];
        }

        if (sceneAdapter.BeginCatalogMerge() && characterCatalog != null)
        {
            IEnumerable<CharacterSO> catalogCustomers = characterCatalog.Characters
                .Where(data => data != null && data.characterType == CharacterType.Customer);
            characters = characters
                .Concat(catalogCustomers)
                .Where(data => data != null)
                .GroupBy(data => data.id)
                .Select(group => group.First())
                .ToArray();
        }

        characters = characters.Where((x) => x != null).OrderBy((x) => x.id).ToArray();
        sceneAdapter.RebuildCharacterIndex(characters);
        spawnDelay ??= new WaitForSeconds(0.3f);
        sceneAdapter.BeginRuntimeInitialization();
        EnsureCharacterPool();
    }

    private void EnsureCharacterPool()
    {
        if (characterPool == null && characterPrefab != null)
        {
            characterPool = new ObjectPool<GameObject>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPoolObject, true, 5, 15);
        }
    }

    public IEnumerator StartSpawn()
    {
        while (!scopeTeardownStarted)
        {
            if (deterministicSimulationPausedForDiagnostics)
            {
                yield return spawnDelay;
                continue;
            }

            EnsureRuntimeState();

            foreach (var item in characters)
            {
                if (TrySpawnCharacter(item.id))
                {
                    break;
                }
            }

            yield return spawnDelay;
        }
    }
    void Update()
    {
        // Scene components can receive an Update between activation and the
        // hierarchy injection build callback. The spawner has no authoritative
        // clock until that callback completes, and it must not advance while
        // its owning scope is being torn down.
        if (scopeTeardownStarted
            || deterministicSimulationPausedForDiagnostics
            || !HasInjectedGameClock)
        {
            return;
        }

        respawnSchedule.Advance(GameDeltaTime);
    }

    public bool DeterministicSimulationPausedForDiagnostics =>
        deterministicSimulationPausedForDiagnostics;

    public void ConfigureDeterministicSimulationForDiagnostics(bool paused)
    {
        if (!Application.isEditor)
        {
            throw new InvalidOperationException(
                "Deterministic spawner simulation is editor-only.");
        }

        deterministicSimulationPausedForDiagnostics = paused;
    }

    internal void PrepareForScopeTeardown()
    {
        if (scopeTeardownStarted)
        {
            return;
        }

        scopeTeardownStarted = true;
        StopAllCoroutines();
        // ObjectPool itself is managed state and is rebuilt after a domain
        // reload. Destroy its inactive inventory now, through the injected
        // factory, so those GameObjects cannot become unowned orphan pools.
        characterPool?.Clear();
        characterPool = null;
    }

    public bool TrySpawnCharacter(int id) =>
        TrySpawnCharacter(id, out _);

    public bool TrySpawnCharacter(
        int id,
        out CharacterSpawnRejection rejection)
    {
        rejection = CharacterSpawnRejection.None;
        EnsureRuntimeState();
        if (ownerRunManagerProvider == null
            || !ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager.CurrentOwnerActor == null)
        {
            rejection = CharacterSpawnRejection.OwnerUnavailable;
            return false;
        }

        if (characterPool == null)
        {
            rejection = CharacterSpawnRejection.PoolUnavailable;
            Debug.LogWarning("캐릭터 프리팹이 없어 캐릭터를 스폰할 수 없습니다.");
            return false;
        }

        if (!sceneAdapter.TryGetCharacter(id, out CharacterSO characterData))
        {
            rejection = CharacterSpawnRejection.DefinitionMissing;
            Debug.LogWarning($"스폰할 캐릭터 데이터를 찾지 못했습니다. id: {id}");
            return false;
        }

        CharacterSpeciesSO species = characterData.species;
        bool recruitmentUnlocked = species != null
            && !string.IsNullOrWhiteSpace(species.homeFactionId)
            && factionContracts.IsContractUnlocked(
                species.homeFactionId,
                FactionContractKind.Recruitment);
        if (!CharacterSpawnRules.IsRecruitmentEligible(
                species != null,
                species?.ownerSelectable == true,
                species?.homeFactionId,
                recruitmentUnlocked))
        {
            rejection = CharacterSpawnRejection.RecruitmentLocked;
            return false;
        }

        RegularCustomerState regularCustomerState = GetRegularCustomerState();
        if (!RegularCustomerService.CanSpawnAsCustomer(characterData, regularCustomerState))
        {
            rejection = CharacterSpawnRejection.RegularCustomerIneligible;
            return false;
        }

        WorldCharacterProfile worldProfile = characterPopulationService.AcquireVisitor(
            characterData,
            respawnSchedule.UnavailableProfileIds);
        if (worldProfile == null)
        {
            rejection = DiagnosePopulationRejection(characterData.id);
            return false;
        }

        if (!TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition))
        {
            worldProfile.isVisiting = false;
            rejection = CharacterSpawnRejection.EntryUnavailable;
            return false;
        }

        GameObject spawnedCharacterGameobject = characterPool.Get();
        spawnedCharacterGameobject.transform.position = GetOutsideSpawnWorldPosition();
        CharacterActor spawnedCharacter = spawnedCharacterGameobject.GetComponent<CharacterActor>();
        if (spawnedCharacter == null)
        {
            Debug.LogWarning("캐릭터 프리팹에 CharacterActor 컴포넌트가 없습니다.");
            characterPool.Release(spawnedCharacterGameobject);
            worldProfile.isVisiting = false;
            rejection = CharacterSpawnRejection.PrefabInvalid;
            return false;
        }

        spawnedCharacter.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        spawnedCharacter.Initialize(characterData);
        characterPopulationService.BindActor(worldProfile, spawnedCharacter);
        RequireCharacterObjectFactory().Publish(spawnedCharacterGameobject);
        StartCoroutine(EnterWhenPrepared(spawnedCharacter, resolvedEntryGridPosition));

        float demandMultiplier = ResolveRunVariableReader().GetGuestDemandMultiplier(characterData.SpeciesTag);
        float respawnTime = characterData.GetRespawnSpeed(ResolveRespawnRandomStream())
            / Mathf.Max(0.1f, demandMultiplier);
        respawnSchedule.Register(
            worldProfile.persistentId,
            id,
            respawnTime);
        return true;
    }

    private CharacterSpawnRejection DiagnosePopulationRejection(int characterDataId)
    {
        WorldCharacterProfile[] matching = characterPopulationService.Profiles
            .Where(profile => profile != null
                && profile.characterDataId == characterDataId)
            .ToArray();
        if (matching.Length == 0)
        {
            return CharacterSpawnRejection.PopulationProfileNotCreated;
        }

        if (matching.Any(profile => profile.isAlive
                && !profile.isStaff
                && !profile.isVisiting
                && !profile.IsReady))
        {
            return CharacterSpawnRejection.PopulationProfilePreparing;
        }

        if (matching.Any(profile => profile.isAlive
                && !profile.isStaff
                && profile.isVisiting))
        {
            return CharacterSpawnRejection.PopulationProfileAlreadyVisiting;
        }

        return CharacterSpawnRejection.PopulationProfileUnavailable;
    }

    private IEnumerator EnterWhenPrepared(
        CharacterActor actor,
        Vector2Int resolvedEntryGridPosition)
    {
        CharacterVisual characterVisual = actor != null ? actor.GetComponent<CharacterVisual>() : null;
        characterVisual?.SetRenderersVisible(false);
        while (actor != null
            && actor.gameObject.activeInHierarchy
            && (actor.Progression == null
                || actor.Progression.ActiveSkills.Count == 0
                || actor.Progression.PassiveSkills.Count == 0))
        {
            yield return null;
        }

        if (actor == null || !actor.gameObject.activeInHierarchy)
        {
            yield break;
        }

        characterPopulationService.RefreshProfile(actor);
        characterVisual?.SetRenderersVisible(true);
        if (actor.TryGetAbility(out AbilityMove move))
        {
            move.StartEnterDungeon(GetEntryDoorWorldPosition(), resolvedEntryGridPosition);
        }
        else
        {
            if (TryGetGrid(out Grid grid))
            {
                actor.transform.position = grid.GetWorldPos(resolvedEntryGridPosition);
            }

            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }
    }

    public Vector3 GetOutsideSpawnWorldPosition()
    {
        if (outsideSpawnPoint != null)
        {
            return outsideSpawnPoint.position;
        }

        // The scene spawner is a catalog/runtime component, not an authored
        // exterior waypoint. Its transform can be tens of world units away from
        // the resolved entrance after grid-origin changes. Using that transform
        // directly made every visitor walk across the whole world outside the
        // grid before the release handoff. Preserve only its authored side as a
        // direction hint and keep the fallback waypoint adjacent to the actual
        // production entrance.
        Vector3 entry = GetEntryDoorWorldPosition();
        Vector3 direction = transform.position - entry;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }
        return entry + direction.normalized * FallbackOutsideSpawnDistance;
    }

    public Vector3 GetEntryDoorWorldPosition()
    {
        if (entryDoorPoint != null)
        {
            return entryDoorPoint.position;
        }

        if (!TryGetGrid(out Grid grid))
        {
            return transform.position;
        }

        if (TryResolveEntranceDoor(grid, out Door entrance))
        {
            return grid.GetWorldPos(entrance.centerPos);
        }

        return grid.GetWorldPos(GetEntryGridPosition());
    }

    public Vector2Int GetEntryGridPosition()
    {
        return TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition)
            ? resolvedEntryGridPosition
            : entryGridPosition;
    }

    public bool TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition)
    {
        IGridSystemProvider provider = ResolveGridSystemProvider();
        if (!provider.TryGetGrid(out Grid grid))
        {
            resolvedEntryGridPosition = entryGridPosition;
            return false;
        }

        if (provider.TryGetManager(out GridSystemManager manager)
            && manager.TryGetEntranceGridPosition(out resolvedEntryGridPosition))
        {
            return true;
        }

        if (grid.IsValidGridPos(entryGridPosition) && grid.IsWalkable(entryGridPosition))
        {
            resolvedEntryGridPosition = entryGridPosition;
            return true;
        }

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell != null
                && cell.AreaType == GridCellAreaType.Entrance
                && grid.IsWalkable(cell.Position))
            {
                resolvedEntryGridPosition = cell.Position;
                return true;
            }
        }

        Vector3 desiredWorldPosition = entryDoorPoint != null ? entryDoorPoint.position : transform.position;
        Vector2Int desiredGridPosition = grid.GetXY(desiredWorldPosition);
        return grid.TryFindNearestWalkablePosition(desiredGridPosition, out resolvedEntryGridPosition);
    }

    private bool TryResolveEntranceDoor(Grid grid, out Door entrance)
    {
        return sceneAdapter.TryResolveEntrance(
            grid,
            buildingWorldQuery,
            entryGridPosition,
            out entrance);
    }

    private RegularCustomerState GetRegularCustomerState()
    {
        return RequireRegularCustomers().State;
    }

    private bool TryGetGrid(out Grid grid)
    {
        return ResolveGridSystemProvider().TryGetGrid(out grid);
    }

    private RegularCustomerRuntime RequireRegularCustomers()
    {
        return regularCustomers
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterSpawner)} requires {nameof(RegularCustomerRuntime)} injection.");
    }

    private IGridSystemProvider ResolveGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(CharacterSpawner)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private IRunVariableRuntimeReader ResolveRunVariableReader()
    {
        return runVariableReader
            ?? throw new InvalidOperationException($"{nameof(CharacterSpawner)} requires {nameof(IRunVariableRuntimeReader)} injection.");
    }

    private IRandomStream ResolveRespawnRandomStream()
    {
        return respawnRandomStream
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterSpawner)} requires {nameof(IRandomStreamProvider)} injection.");
    }

    private ICharacterSpawnObjectFactory RequireCharacterObjectFactory()
    {
        return characterObjectFactory
            ?? throw new InvalidOperationException($"{nameof(CharacterSpawner)} requires {nameof(ICharacterSpawnObjectFactory)} injection.");
    }

    public void Respawned(CharacterRespawnData data)
    {
        if (data != null)
        {
            respawnSchedule.Remove(data.id);
        }
    }
    private GameObject CreatePooledItem()
    {
        return RequireCharacterObjectFactory().CreateInactive(characterPrefab);
    }
    private void OnTakeFromPool(GameObject poolGo)
    {
        poolGo.SetActive(false);
    }
    private void OnReturnedToPool(GameObject poolGo)
    {
        CharacterActor actor = poolGo.GetComponent<CharacterActor>();
        if (actor != null)
        {
            actor.SetLifecycleState(CharacterLifecycleState.Despawned);
        }

        poolGo.SetActive(false);
    }
    private void OnDestroyPoolObject(GameObject poolGo)
    {
        RequireCharacterObjectFactory().Destroy(poolGo);
    }

    /// <summary>
    /// Retires a transient customer whose population binding belonged to the
    /// character world that was replaced by a committed restore. This boundary
    /// deliberately does not call CharacterPopulationService.ReleaseVisitor:
    /// the restored population aggregate is already authoritative at commit.
    /// </summary>
    public bool RetireVisitorForWorldRestore(
        CharacterActor actor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null)
        {
            return true;
        }

        CharacterIdentity identity = actor.Identity;
        if (identity == null || identity.Data == null)
        {
            failureReason = "restore retirement requires an initialized character";
            return false;
        }
        if (actor.IsOwner || identity.CharacterType != CharacterType.Customer)
        {
            failureReason =
                $"restore retirement only accepts transient customers: "
                + $"id={identity.PersistentId};type={identity.CharacterType};owner={actor.IsOwner}";
            return false;
        }

        // ObjectPool.Release invokes OnReturnedToPool synchronously. The
        // inactive guard therefore also makes repeated restore cleanup
        // idempotent without maintaining a second ownership registry. An
        // active Despawned actor is an invalid half-retired state and must not
        // be silently accepted.
        if (!actor.gameObject.activeSelf)
        {
            return true;
        }
        if (actor.CurrentLifecycleState == CharacterLifecycleState.Despawned)
        {
            failureReason =
                "restore retirement found an active Despawned customer: "
                + identity.PersistentId;
            return false;
        }

        if (characterPool != null)
        {
            characterPool.Release(actor.gameObject);
        }
        else
        {
            actor.SetLifecycleState(CharacterLifecycleState.Despawned);
            actor.gameObject.SetActive(false);
        }
        return true;
    }

    public IEnumerator Interact(CharacterActor actor) =>
        Interact(actor?.BuildingVisitor);

    public IEnumerator Interact(IBuildingVisitorPort visitor)
    {
        // The exit traversal changes lifecycle state at its terminal boundary.
        // That transition releases the movement coroutine which invoked this
        // method, so the authoritative population/pool handoff must happen
        // eagerly when Interact is called, not on the iterator's first MoveNext.
        CompleteInteraction(visitor);
        return YieldAfterInteraction();
    }

    private void CompleteInteraction(IBuildingVisitorPort visitor)
    {
        if (visitor == null)
        {
            return;
        }
        if (!CharacterBuildingVisitorAdapter.TryGetActor(
                visitor,
                out CharacterActor actor))
        {
            throw new InvalidOperationException(
                "CharacterSpawner requires the Character visitor adapter.");
        }

        CharacterIdentity identity = actor != null ? actor.Identity : null;
        if (identity == null || identity.Data == null) return;

        EnsureRuntimeState();
        bool isStaffProfile = characterPopulationService != null
            && characterPopulationService.TryGetProfile(actor, out WorldCharacterProfile profile)
            && profile != null
            && profile.isStaff;
        // AbilityWork is injected on the shared character prefab, so component
        // presence is not staff ownership. Treating it as a work-role authority
        // reactivated every real Customer at the exit instead of releasing the
        // visitor profile and returning the actor to the pool. Population staff
        // state and the persistent NPC identity are the production authorities.
        if (identity.CharacterType == CharacterType.NPC
            || isStaffProfile)
        {
            if (isStaffProfile)
            {
                actor.characterType = CharacterType.NPC;
                actor.Identity?.SetCharacterType(CharacterType.NPC);
            }

            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.gameObject.SetActive(true);
            characterPopulationService?.RefreshProfile(actor);
            return;
        }

        string profileId = identity.PersistentId;
        visitorExitHandoffAttemptCount++;
        lastVisitorExitPersistentId = profileId ?? string.Empty;
        lastVisitorExitHandoffStage = "visitor-exit:handoff-started";
        respawnSchedule.MarkDisabled(profileId);

        characterPopulationService?.ReleaseVisitor(actor);
        lastVisitorExitHandoffStage = "visitor-exit:population-released";

        if (characterPool != null)
        {
            characterPool.Release(actor.gameObject);
            lastVisitorExitHandoffStage = "visitor-exit:pool-released";
        }
        else
        {
            actor.SetLifecycleState(CharacterLifecycleState.Despawned);
            actor.gameObject.SetActive(false);
            lastVisitorExitHandoffStage = "visitor-exit:deactivated";
        }

        visitorExitHandoffCompletedCount++;
    }

    private static IEnumerator YieldAfterInteraction()
    {
        yield return null;
    }
}
