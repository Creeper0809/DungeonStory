using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public static class DungeonEntranceGridResolver
{
    public static bool TryResolve(
        Grid grid,
        Vector2Int preferredInsidePosition,
        out Door entrance)
    {
        entrance = null;
        if (grid == null)
        {
            return false;
        }

        entrance = grid.GetCells()
            .Select(cell => cell?.GetBuildingInlayer(GridLayer.Building))
            .OfType<Door>()
            .Where(door => door != null
                && door.IsDungeonEntrance
                && !door.isDestroy
                && door.BuildingData != null
                && !door.BuildingData.IsInteriorDoor)
            .Distinct()
            .OrderBy(door => Mathf.Abs(door.centerPos.x - preferredInsidePosition.x)
                + Mathf.Abs(door.centerPos.y - preferredInsidePosition.y))
            .FirstOrDefault();
        return entrance != null;
    }
}

public class CharacterSpawner : BuildableObject,IInteractable
{
    public CharacterSO[] characters;
    public GameObject characterPrefab;
    [SerializeField] private Transform outsideSpawnPoint;
    [SerializeField] private Transform entryDoorPoint;
    [SerializeField] private Vector2Int entryGridPosition = new Vector2Int(4, 0);

    private float timer;
    private Dictionary<string, CharacterRespawnData> respawnDict = new Dictionary<string, CharacterRespawnData>();
    private Dictionary<int, CharacterSO> charactersById = new Dictionary<int, CharacterSO>();
    public IObjectPool<GameObject> characterPool;
    private WaitForSeconds spawnDelay = new WaitForSeconds(0.3f);
    private bool spawnRoutineStarted;
    private IRegularCustomerRuntimeProvider regularCustomerRuntimeProvider;
    private IGridSystemProvider gridSystemProvider;
    private IRunVariableRuntimeReader runVariableReader;
    private ICharacterSpawnObjectFactory characterObjectFactory;
    private IRunCharacterCatalog characterCatalog;
    private ICharacterPopulationService characterPopulationService;
    private IFactionRuntime factionRuntime;
    private IOwnerRunManagerProvider ownerRunManagerProvider;
    private IBuildingWorldQuery buildingWorldQuery;
    private IRandomStream respawnRandomStream;
    private bool catalogCustomersMerged;
    private bool runtimeStateInitialized;
    private int cachedEntranceBuildingVersion = -1;
    private Door cachedEntranceDoor;

    [Inject]
    public void Construct(
        IRegularCustomerRuntimeProvider regularCustomerRuntimeProvider,
        IGridSystemProvider gridSystemProvider,
        IRunVariableRuntimeReader runVariableReader,
        ICharacterSpawnObjectFactory characterObjectFactory,
        IRunCharacterCatalog characterCatalog,
        ICharacterPopulationService characterPopulationService,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        IBuildingWorldQuery buildingWorldQuery,
        IRandomStreamProvider randomStreamProvider,
        IFactionRuntime factionRuntime = null)
    {
        this.regularCustomerRuntimeProvider = regularCustomerRuntimeProvider
            ?? throw new ArgumentNullException(nameof(regularCustomerRuntimeProvider));
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
        this.factionRuntime = factionRuntime;
        respawnRandomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-spawner");
        catalogCustomersMerged = false;
        runtimeStateInitialized = false;
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

        if (!spawnRoutineStarted)
        {
            spawnRoutineStarted = true;
            StartCoroutine(StartSpawn());
        }
    }

    private void EnsureRuntimeState()
    {
        if (runtimeStateInitialized)
        {
            EnsureCharacterPool();
            return;
        }

        if (characters == null)
        {
            characters = new CharacterSO[0];
        }

        if (!catalogCustomersMerged && characterCatalog != null)
        {
            IEnumerable<CharacterSO> catalogCustomers = characterCatalog.Characters
                .Where(data => data != null && data.characterType == CharacterType.Customer);
            characters = characters
                .Concat(catalogCustomers)
                .Where(data => data != null)
                .GroupBy(data => data.id)
                .Select(group => group.First())
                .ToArray();
            catalogCustomersMerged = true;
        }

        characters = characters.Where((x) => x != null).OrderBy((x) => x.id).ToArray();
        charactersById = characters.GroupBy((x) => x.id).ToDictionary((x) => x.Key, (x) => x.First());
        respawnDict ??= new Dictionary<string, CharacterRespawnData>();
        spawnDelay ??= new WaitForSeconds(0.3f);
        runtimeStateInitialized = true;
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
        while (true)
        {
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
        if (respawnDict == null) return;

        timer += GameDeltaTime;
        string respawnedId = null;
        foreach(var item in respawnDict)
        {
            if (item.Value.CheckResapwn(timer))
            {
                respawnedId = item.Key;
                break;
            }
        }
        if (!string.IsNullOrWhiteSpace(respawnedId))
        {
            respawnDict.Remove(respawnedId);
        }
    }
    public bool TrySpawnCharacter(int id)
    {
        EnsureRuntimeState();
        if (ownerRunManagerProvider == null
            || !ownerRunManagerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager.CurrentOwnerActor == null)
        {
            return false;
        }

        if (characterPool == null)
        {
            Debug.LogWarning("캐릭터 프리팹이 없어 캐릭터를 스폰할 수 없습니다.");
            return false;
        }

        if (!charactersById.TryGetValue(id, out CharacterSO characterData))
        {
            Debug.LogWarning($"스폰할 캐릭터 데이터를 찾지 못했습니다. id: {id}");
            return false;
        }

        CharacterSpeciesSO species = characterData.species;
        if (species != null
            && !species.ownerSelectable
            && (string.IsNullOrWhiteSpace(species.homeFactionId)
                || factionRuntime?.IsContractUnlocked(
                    species.homeFactionId,
                    FactionContractKind.Recruitment) != true))
        {
            return false;
        }

        RegularCustomerState regularCustomerState = GetRegularCustomerState();
        if (!RegularCustomerService.CanSpawnAsCustomer(characterData, regularCustomerState))
        {
            return false;
        }

        WorldCharacterProfile worldProfile = characterPopulationService.AcquireVisitor(
            characterData,
            respawnDict.Keys);
        if (worldProfile == null)
        {
            return false;
        }

        if (!TryGetEntryGridPosition(out Vector2Int resolvedEntryGridPosition))
        {
            worldProfile.isVisiting = false;
            return false;
        }

        GameObject spawnedCharacterGameobject = characterPool.Get();
        RequireCharacterObjectFactory().Inject(spawnedCharacterGameobject);
        spawnedCharacterGameobject.transform.position = GetOutsideSpawnWorldPosition();
        CharacterActor spawnedCharacter = spawnedCharacterGameobject.GetComponent<CharacterActor>();
        if (spawnedCharacter == null)
        {
            Debug.LogWarning("캐릭터 프리팹에 CharacterActor 컴포넌트가 없습니다.");
            characterPool.Release(spawnedCharacterGameobject);
            worldProfile.isVisiting = false;
            return false;
        }

        spawnedCharacter.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        spawnedCharacter.Initialize(characterData);
        characterPopulationService.BindActor(worldProfile, spawnedCharacter);
        StartCoroutine(EnterWhenPrepared(spawnedCharacter, resolvedEntryGridPosition));

        float demandMultiplier = ResolveRunVariableReader().GetGuestDemandMultiplier(characterData.SpeciesTag);
        float respawnTime = characterData.GetRespawnSpeed(ResolveRespawnRandomStream())
            / Mathf.Max(0.1f, demandMultiplier);
        respawnDict[worldProfile.persistentId] = new CharacterRespawnData(
            worldProfile.persistentId,
            id,
            respawnTime);
        return true;
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

        return transform.position;
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
        entrance = null;
        if (grid == null || buildingWorldQuery == null)
        {
            return DungeonEntranceGridResolver.TryResolve(
                grid,
                entryGridPosition,
                out entrance);
        }

        int buildingVersion = buildingWorldQuery.BuildingVersion;
        if (cachedEntranceBuildingVersion == buildingVersion)
        {
            entrance = cachedEntranceDoor;
            return entrance != null && !entrance.isDestroy;
        }

        cachedEntranceBuildingVersion = buildingVersion;
        cachedEntranceDoor = null;
        int bestDistance = int.MaxValue;
        IReadOnlyList<BuildableObject> buildings = buildingWorldQuery.Buildings;
        for (int index = 0; index < buildings.Count; index++)
        {
            if (buildings[index] is not Door candidate
                || !candidate.IsDungeonEntrance
                || candidate.isDestroy
                || candidate.BuildingData == null
                || candidate.BuildingData.IsInteriorDoor)
            {
                continue;
            }

            int distance = Mathf.Abs(candidate.centerPos.x - entryGridPosition.x)
                + Mathf.Abs(candidate.centerPos.y - entryGridPosition.y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            cachedEntranceDoor = candidate;
        }

        entrance = cachedEntranceDoor;
        return entrance != null;
    }

    private RegularCustomerState GetRegularCustomerState()
    {
        return ResolveRegularCustomerRuntimeProvider().TryGetRuntime(out RegularCustomerRuntime runtime)
            ? runtime.State
            : null;
    }

    private bool TryGetGrid(out Grid grid)
    {
        return ResolveGridSystemProvider().TryGetGrid(out grid);
    }

    private IRegularCustomerRuntimeProvider ResolveRegularCustomerRuntimeProvider()
    {
        return regularCustomerRuntimeProvider
            ?? throw new InvalidOperationException($"{nameof(CharacterSpawner)} requires {nameof(IRegularCustomerRuntimeProvider)} injection.");
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
        respawnDict.Remove(data.id);
    }
    private GameObject CreatePooledItem()
    {
        return RequireCharacterObjectFactory().Create(characterPrefab);
    }
    private void OnTakeFromPool(GameObject poolGo)
    {
        poolGo.SetActive(true);
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
    public IEnumerator Interact(CharacterActor actor)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        if (identity == null || identity.Data == null) yield break;

        EnsureRuntimeState();
        bool isStaffProfile = characterPopulationService != null
            && characterPopulationService.TryGetProfile(actor, out WorldCharacterProfile profile)
            && profile != null
            && profile.isStaff;
        if (CharacterWorkRoleUtility.TryGetWork(actor, out _)
            || identity.CharacterType == CharacterType.NPC
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
            yield break;
        }

        string profileId = identity.PersistentId;
        if (respawnDict.TryGetValue(profileId, out CharacterRespawnData respawnData))
        {
            respawnData.StartCheckRespawn(timer);
        }

        characterPopulationService?.ReleaseVisitor(actor);

        if (characterPool != null)
        {
            characterPool.Release(actor.gameObject);
        }
        else
        {
            actor.SetLifecycleState(CharacterLifecycleState.Despawned);
            actor.gameObject.SetActive(false);
        }

        yield return null;
    }
}
public class CharacterRespawnData
{
    public string id;
    public int characterDataId;
    public float lastDisabledTime;
    public float respawnTime;
    public bool isDiabled;
    public CharacterRespawnData(string id, int characterDataId, float respawnTime)
    {
        this.respawnTime = respawnTime;
        this.id = id;
        this.characterDataId = characterDataId;
        isDiabled = false;
    }
    public void StartCheckRespawn(float lastDisabledTime)
    {
        isDiabled = true;
        this.lastDisabledTime = lastDisabledTime;
    }
    public bool CheckResapwn(float time)
    {
        if (!isDiabled) return false;
        if ((time - lastDisabledTime) < respawnTime) return false;
        return true;
    }
}
