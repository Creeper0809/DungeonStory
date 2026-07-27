using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class InvasionIntruderDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Invasion/Run P1 Intruder Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 intruder scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("침입자 에셋", VerifyIntruderAsset, errors);
        RunScenario("탐색성과 목표 보정", VerifyExplorationBias, errors);
        RunScenario("패턴별 경로와 시설 우선순위", VerifyPatternRouting, errors);
        RunScenario("패턴별 근접 파손 대상 엄수", VerifyPatternDamagePreference, errors);
        RunScenario("시설 파손 보조 목표", VerifyFacilityDamage, errors);
        RunScenario("최종 교전과 런 종료", VerifyFinalCombatEndsRun, errors);
        RunScenario("Regular and boss owner damage tuning", VerifyOwnerDamageTuning, errors);
        RunScenario("Final invasion withdraws stale regular intruders", VerifyFinalInvasionWithdrawal, errors);
        RunScenario("Final defense rally uses the shared entrance floor", VerifyFinalDefenseRallyPlan, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 intruder scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyIntruderAsset()
    {
        CharacterSO intruder = LoadIntruder();
        return intruder != null
            && intruder.characterType == CharacterType.Intruder
            && intruder.role == CharacterRole.Regular
            && intruder.id == 2001
            && intruder.characterSprite != null
            && intruder.moveSpeed > 0f;
    }

    private static bool VerifyExplorationBias()
    {
        Grid grid = new Grid(8, 1);
        for (int x = 0; x < grid.width; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int ownerPosition = new Vector2Int(7, 0);
        IGridPathSearchBroker pathSearchBroker = CreatePathSearchBroker();
        IRandomStream pathRandom =
            new RandomStreamProvider(71).Get("exploration-bias");
        Queue<GridMoveStep> earlyPath = InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            out bool earlyDirect);
        Queue<GridMoveStep> latePath = InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            1f,
            pathSearchBroker,
            pathRandom,
            out bool lateDirect);

        bool earlyExplores = !earlyDirect
            && earlyPath.Count > 0
            && earlyPath.Last().To != ownerPosition;
        bool lateTargetsOwner = lateDirect
            && latePath.Count > 0
            && latePath.Last().To == ownerPosition;

        return earlyExplores && lateTargetsOwner;
    }

    private static bool VerifyPatternRouting()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(14);
        BuildableObject food = world.Place("D01_간이화덕", new Vector2Int(3, 0));
        BuildableObject research = world.Place("Q01_연구책상", new Vector2Int(7, 0));
        BuildableObject defense = world.Place("P1_SpikeTrap", new Vector2Int(10, 0));
        Vector2Int start = Vector2Int.zero;
        Vector2Int ownerPosition = new Vector2Int(13, 0);
        IGridPathSearchBroker pathSearchBroker = CreatePathSearchBroker();
        IRandomStream pathRandom =
            new RandomStreamProvider(83).Get("pattern-routing");

        Queue<GridMoveStep> breakerPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            InvasionIntruderPatternCatalog.Get(InvasionIntruderPatternIds.Breaker),
            out bool breakerDirect,
            out BuildableObject breakerTarget);
        Queue<GridMoveStep> plundererPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            InvasionIntruderPatternCatalog.Get(InvasionIntruderPatternIds.Plunderer),
            out bool plundererDirect,
            out BuildableObject plundererTarget);
        Queue<GridMoveStep> ambusherPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0.4f,
            pathSearchBroker,
            pathRandom,
            InvasionIntruderPatternCatalog.Get(InvasionIntruderPatternIds.Ambusher),
            out bool ambusherDirect,
            out BuildableObject ambusherTarget);
        Queue<GridMoveStep> stragglerPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0.4f,
            pathSearchBroker,
            pathRandom,
            InvasionIntruderPatternCatalog.Get(InvasionIntruderPatternIds.Straggler),
            out bool stragglerDirect,
            out BuildableObject stragglerTarget);

        BuildableObject expectedValuable = new[] { food, research }
            .OrderByDescending(candidate => candidate.GetConstructionCost())
            .First();
        bool valid = !breakerDirect
            && breakerTarget == defense
            && breakerPath.Count > 0
            && !plundererDirect
            && plundererTarget == expectedValuable
            && plundererPath.Count > 0
            && ambusherDirect
            && ambusherTarget == null
            && ambusherPath.Count > 0
            && ambusherPath.Last().To == ownerPosition
            && !stragglerDirect
            && stragglerTarget == null
            && stragglerPath.Count > 0
            && stragglerPath.Last().To != ownerPosition;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Pattern routing mismatch: breaker={breakerTarget?.name}:{breakerPath.Count}:{breakerDirect}; "
                + $"plunderer={plundererTarget?.name}/{expectedValuable?.name}:{plundererPath.Count}:{plundererDirect}; "
                + $"ambusher={ambusherTarget?.name}:{ambusherPath.Count}:{ambusherDirect}; "
                + $"straggler={stragglerTarget?.name}:{stragglerPath.Count}:{stragglerDirect}; "
                + $"costs={food.GetConstructionCost()}/{research.GetConstructionCost()}.");
        }

        return true;
    }

    private static bool VerifyFacilityDamage()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        BuildableObject facility = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        BuildableObject secondFacility = world.Place("Q01_연구책상", new Vector2Int(5, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));

        GameEventBus eventBus = new GameEventBus();
        SetPrivateField(runtime, "gameEventBus", eventBus);
        CountingFacilityDamageListener listener = new CountingFacilityDamageListener(eventBus);
        bool damaged = runtime.TryDamageNearbyFacility(world.Grid);
        facility.SetDamaged(false);
        intruder.transform.position = world.Grid.GetWorldPos(new Vector2Int(4, 0));
        bool damagedAgain = runtime.TryDamageNearbyFacility(world.Grid);
        bool valid = damaged
            && !damagedAgain
            && !facility.IsDamaged
            && !secondFacility.IsDamaged
            && runtime.FacilityDamageCount == 1
            && listener.Count == 1
            && listener.LastFacility == facility;

        listener.Dispose();
        return valid;
    }

    private static bool VerifyPatternDamagePreference()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        BuildableObject facility = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        BuildableObject defense = world.Place("P1_SpikeTrap", new Vector2Int(6, 0));

        bool breakerIgnoredFacility = !InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(1, 0),
            InvasionIntruderTargetPreference.DefenseFacility,
            null,
            out _);
        bool plundererIgnoredDefense = !InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(5, 0),
            InvasionIntruderTargetPreference.ValuableFacility,
            null,
            out _);
        bool plundererFoundFacility = InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(1, 0),
            InvasionIntruderTargetPreference.ValuableFacility,
            null,
            out BuildableObject valuableTarget)
            && valuableTarget == facility;
        bool breakerFoundDefense = InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(5, 0),
            InvasionIntruderTargetPreference.DefenseFacility,
            null,
            out BuildableObject defenseTarget)
            && defenseTarget == defense;

        return breakerIgnoredFacility
            && plundererIgnoredDefense
            && plundererFoundFacility
            && breakerFoundDefense;
    }

    private static bool VerifyFinalCombatEndsRun()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        CharacterSO ownerData = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Orc.asset");
        if (ownerData == null)
        {
            return false;
        }

        GameObject managerObject = new GameObject("Intruder Final Combat OwnerRunManager");
        world.Track(managerObject);
        OwnerRunManager manager = managerObject.AddComponent<OwnerRunManager>();
        manager.ConstructOwnerRunManager(
            new FixedOwnerCandidateCatalog(ownerData),
            new ScenarioOwnerCharacterFactory(world),
            CharacterAiEditorTestDependencies.GameEvents);
        manager.SelectOwner(ownerData);

        CharacterActor owner = manager.CurrentOwnerActor;
        if (owner == null)
        {
            return false;
        }

        SetPrivateField(
            owner.GetComponent<CharacterStats>(),
            "ownerRunLifecycleService",
            new ScenarioOwnerRunLifecycleService(manager));

        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));
        SetPrivateField(runtime, "gameEventBus", new GameEventBus());
        SetPrivateField(runtime, "settings", new InvasionIntruderSettings
        {
            finalCombatDamage = owner.MaxHealth + 10f,
            finalCombatWindupSeconds = 0f
        });

        runtime.ApplyFinalCombat(CharacterActor.From(owner));

        bool valid = owner.IsDead
            && manager.IsRunEnded
            && runtime.State == InvasionIntruderState.FinalCombat;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Final combat mismatch: ownerType={owner.characterType}, "
                + $"ownerRole={owner.Role}, health={owner.CurrentHealth}/{owner.MaxHealth}, "
                + $"dead={owner.IsDead}, runEnded={manager.IsRunEnded}, state={runtime.State}.");
        }

        return true;
    }

    private static bool VerifyOwnerDamageTuning()
    {
        float normal = InvasionOwnerDamageTuning.Resolve(45f, 45f, false, 0f, 0f);
        float boss = InvasionOwnerDamageTuning.Resolve(45f, 45f, true, 0f, 0f);
        float armedNormal = InvasionOwnerDamageTuning.Resolve(45f, 60.75f, false, 0f, 0f);
        float armedBoss = InvasionOwnerDamageTuning.Resolve(45f, 60.75f, true, 0f, 0f);

        return Mathf.Approximately(normal, 10f)
            && Mathf.Approximately(boss, 90f)
            && Mathf.Approximately(armedNormal, 13.5f)
            && Mathf.Approximately(armedBoss, 121.5f);
    }

    private static bool VerifyFinalInvasionWithdrawal()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        GameObject directorObject = new GameObject("Final Invasion Withdrawal Director");
        world.Track(directorObject);
        InvasionDirectorRuntime director = directorObject.AddComponent<InvasionDirectorRuntime>();
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));

        FieldInfo activeField = typeof(InvasionDirectorRuntime).GetField(
            "activeIntruders",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (activeField?.GetValue(director) is not List<InvasionIntruderRuntime> active)
        {
            return false;
        }

        active.Add(runtime);
        int withdrawn = director.WithdrawActiveIntrudersForFinalInvasion();
        return withdrawn == 1
            && director.ActiveIntruders.Count == 0
            && runtime == null;
    }

    public static bool VerifyFinalDefenseRallyPlan()
    {
        Grid grid = new Grid(12, 1);
        for (int x = 0; x < 10; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        Vector2Int entry = Vector2Int.zero;
        Vector2Int owner = new Vector2Int(10, 0);
        bool planned = FinalDefenseRallyPlanner.TryCreate(
            grid,
            entry,
            owner,
            CreatePathSearchBroker(),
            out FinalDefenseRallyPlan plan);
        return planned
            && !grid.IsWalkable(owner)
            && plan.Target == new Vector2Int(9, 0)
            && plan.Target.y == entry.y
            && plan.IntruderSteps.Count == 9
            && plan.OwnerSteps.Count == 1
            && plan.IntruderSteps.All(step => step.IsValid && step.To.y == entry.y)
            && plan.OwnerSteps.All(step => step.IsValid && step.To.y == entry.y);
    }

    private static IGridPathSearchBroker CreatePathSearchBroker()
    {
        GridPathSearchBroker broker = new GridPathSearchBroker(new UnityGameClock());
        broker.BeginFrame(128, enforceBudget: false);
        return broker;
    }

    private static CharacterSO LoadIntruder()
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset");
    }

    private static void AddHallway(Grid grid, Vector2Int position)
    {
        grid.RegisterOccupant(
            new TestHallwayOccupant(),
            GridLayer.Hallway,
            new List<Vector2Int> { position },
            false);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private sealed class IntruderScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly GameObject gridSystemObject;

        public IntruderScenarioWorld(int width)
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;

            Grid = new Grid(width, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                AddHallway(Grid, new Vector2Int(x, 0));
            }

            gridSystemObject = new GameObject("Intruder Scenario GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public void Track(GameObject obj)
        {
            if (obj != null && !objects.Contains(obj))
            {
                objects.Add(obj);
            }
        }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            buildingData = buildingData != null
                ? buildingData
                : AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
            if (buildingData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            building.SetGrid(Grid);
            CharacterAiEditorTestDependencies.Inject(building);
            CharacterAiEditorTestDependencies.InjectShop(building.GetComponent<Shop>());
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            return building;
        }

        public CharacterActor CreateIntruder(Vector2Int position)
        {
            return CreateCharacter(LoadIntruder(), position, "Intruder Scenario Character");
        }

        public CharacterActor CreateCharacter(
            CharacterSO characterData,
            Vector2Int position,
            string objectName)
        {
            if (characterData == null)
            {
                throw new ArgumentNullException(nameof(characterData));
            }

            GameObject obj = new GameObject(objectName);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            CharacterAwakeMethod?.Invoke(character, null);
            character.RefreshAbilityCache();
            character.Initialization(characterData);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            obj.transform.position = Grid.GetWorldPos(position);
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class FixedOwnerCandidateCatalog : IOwnerCandidateCatalog
    {
        private readonly IReadOnlyCollection<CharacterSO> candidates;

        public FixedOwnerCandidateCatalog(CharacterSO ownerData)
        {
            candidates = new[] { ownerData };
        }

        public IReadOnlyCollection<CharacterSO> OwnerCandidates => candidates;
    }

    private sealed class ScenarioOwnerCharacterFactory : IOwnerCharacterFactory
    {
        private readonly IntruderScenarioWorld world;

        public ScenarioOwnerCharacterFactory(IntruderScenarioWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public CharacterActor CreateOwner(
            CharacterSO ownerData,
            GameObject ownerPrefab,
            Transform ownerSpawnPoint,
            Vector2Int ownerSpawnGridPosition)
        {
            CharacterActor owner = world.CreateCharacter(
                ownerData,
                ownerSpawnGridPosition,
                "Intruder Scenario Owner");
            if (ownerSpawnPoint != null)
            {
                owner.transform.position = ownerSpawnPoint.position;
            }

            return owner;
        }
    }

    private sealed class ScenarioOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        private readonly OwnerRunManager manager;

        public ScenarioOwnerRunLifecycleService(OwnerRunManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
            manager.HandleOwnerDeath(owner, reason);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class CountingFacilityDamageListener : IDisposable
    {
        private readonly IDisposable subscription;

        public int Count { get; private set; }
        public BuildableObject LastFacility { get; private set; }

        public CountingFacilityDamageListener(IGameEventBus eventBus)
        {
            subscription = (eventBus ?? throw new ArgumentNullException(nameof(eventBus)))
                .Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
        {
            Count++;
            LastFacility = eventType.facility;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
