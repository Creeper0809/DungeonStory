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

public class CharacterSpawner : BuildableObject,IInteractable
{
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
        respawnSchedule.Advance(GameDeltaTime);
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

        if (!sceneAdapter.TryGetCharacter(id, out CharacterSO characterData))
        {
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
            return false;
        }

        RegularCustomerState regularCustomerState = GetRegularCustomerState();
        if (!RegularCustomerService.CanSpawnAsCustomer(characterData, regularCustomerState))
        {
            return false;
        }

        WorldCharacterProfile worldProfile = characterPopulationService.AcquireVisitor(
            characterData,
            respawnSchedule.UnavailableProfileIds);
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
    public IEnumerator Interact(CharacterActor actor) =>
        Interact(actor?.BuildingVisitor);

    public IEnumerator Interact(IBuildingVisitorPort visitor)
    {
        if (visitor == null)
        {
            yield break;
        }
        if (!CharacterBuildingVisitorAdapter.TryGetActor(
                visitor,
                out CharacterActor actor))
        {
            throw new InvalidOperationException(
                "CharacterSpawner requires the Character visitor adapter.");
        }

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
        respawnSchedule.MarkDisabled(profileId);

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
