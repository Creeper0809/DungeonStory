using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PriorityCommandDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Priority Command Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 priority command scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("선택 캐릭터 상태", VerifySelectionState, errors);
        RunScenario("파손 시설 수리 지시", VerifyDamagedFacilityResolvesRepair, errors);
        RunScenario("재고 부족 시설 보충 지시", VerifyEmptyStockResolvesRestock, errors);
        RunScenario("연구 시설 연구 지시", VerifyResearchFacilityResolvesResearch, errors);
        RunScenario("우선 지시가 꺼진 우선순위 우회", VerifyDirectCommandBypassesOffPriority, errors);
        RunScenario("도달 불가 실패 로그", VerifyUnreachableCommandFails, errors);
        RunScenario("침입자 제압 우선 지정", VerifySuppressPriorityCommand, errors);

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
            Debug.Log("P1 priority command scenarios passed.");
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

    private static bool VerifySelectionState()
    {
        GameObject controllerObject = new GameObject("Priority Command Controller");
        OwnerCommandController controller = controllerObject.AddComponent<OwnerCommandController>();
        CharacterActor actor = CreateCharacter("Owner_Orc");

        controller.OnTriggerEvent(new InfoFeedEvent(CharacterActor.From(actor)));
        bool valid = controller.SelectedActor == CharacterActor.From(actor);

        Object.DestroyImmediate(actor.gameObject);
        Object.DestroyImmediate(controllerObject);
        return valid;
    }

    private static bool VerifyDamagedFacilityResolvesRepair()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld();
        CharacterActor actor = CreateCharacter("Owner_Orc");
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        restRoom.SetDamaged(true);

        bool valid = WorkCommandResolver.TryResolveFacilityCommand(CharacterActor.From(actor), restRoom, out WorkTypeId workTypeId, out _)
            && workTypeId == BuiltInWorkTypeIds.Repair;

        Object.DestroyImmediate(actor.gameObject);
        return valid;
    }

    private static bool VerifyEmptyStockResolvesRestock()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld();
        CharacterActor actor = CreateCharacter("Owner_Slime");
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        ClearShopStock(shop);

        bool valid = WorkCommandResolver.TryResolveFacilityCommand(CharacterActor.From(actor), shop, out WorkTypeId workTypeId, out _)
            && workTypeId == BuiltInWorkTypeIds.Restock;

        Object.DestroyImmediate(actor.gameObject);
        return valid;
    }

    private static bool VerifyResearchFacilityResolvesResearch()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld();
        CharacterActor actor = CreateCharacter("Owner_Vampire");
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));

        bool valid = WorkCommandResolver.TryResolveFacilityCommand(CharacterActor.From(actor), lab, out WorkTypeId workTypeId, out _)
            && workTypeId == BuiltInWorkTypeIds.Research;

        Object.DestroyImmediate(actor.gameObject);
        return valid;
    }

    private static bool VerifyDirectCommandBypassesOffPriority()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld();
        CharacterActor actor = CreateCharacter("Owner_Slime");
        AbilityWork work = actor.GetAbility<AbilityWork>();
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        IWarehouseFacility warehouse =
            world.Place("P1_Warehouse", new Vector2Int(8, 0))
                as IWarehouseFacility;
        ClearShopStock(shop);
        warehouse?.Inventory.SeedPhysicalStockForTest(StockCategory.Food, 5);
        work.SetWorkPriority(BuiltInWorkTypeIds.Restock, WorkPriorityLevel.Off);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool valid = work.TrySetPriorityWorkTarget(
                shop,
                BuiltInWorkTypeIds.Restock,
                search,
                out _)
            && work.PriorityWorkTarget == shop
            && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Restock;

        Object.DestroyImmediate(actor.gameObject);
        return valid;
    }

    private static bool VerifyUnreachableCommandFails()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld(12, 5);
        CharacterActor actor = CreateCharacter("Owner_Vampire");
        AbilityWork work = actor.GetAbility<AbilityWork>();
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(10, 0));

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool valid = !work.TrySetPriorityWorkTarget(lab, BuiltInWorkTypeIds.Research, search, out string errorMessage)
            && errorMessage == "도달할 수 없는 대상입니다"
            && actor.Log.Any((log) => log.Contains("우선 지정 실패")) == false;

        Object.DestroyImmediate(actor.gameObject);
        return valid;
    }

    private static bool VerifySuppressPriorityCommand()
    {
        using CommandScenarioWorld world = new CommandScenarioWorld();
        CharacterActor actor = CreateCharacter("Owner_Orc");
        actor.transform.position = world.Grid.GetWorldPos(Vector2Int.zero);
        BuildableObject destination = world.Place("P1_LowFoodShop", new Vector2Int(4, 0));
        CharacterActor intruder = CreateIntruder("Intruder_Breakthrough", world.Grid.GetWorldPos(new Vector2Int(4, 0)));
        AbilityWork work = actor.GetAbility<AbilityWork>();
        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        AIWork action = ScriptableObject.CreateInstance<AIWork>();

        bool valid = WorkCommandResolver.TryResolveSuppressCommand(
                CharacterActor.From(actor),
                CharacterActor.From(intruder),
                _ => false,
                out _)
            && work.TrySetPrioritySuppressTarget(CharacterActor.From(intruder), search, out _)
            && work.PrioritySuppressActor == CharacterActor.From(intruder)
            && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Guard
            && work.TryGetPrioritySuppressDestination(search, out BuildableObject suppressDestination)
            && suppressDestination == destination
            && action.GetDestinationCandidates(CharacterActor.From(actor), search).Contains(destination);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(actor.gameObject);
        Object.DestroyImmediate(intruder.gameObject);
        return valid;
    }

    private static CharacterActor CreateCharacter(string ownerAssetName)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            $"Assets/Resources/SO/Character/Owners/{ownerAssetName}.asset");

        GameObject obj = new GameObject(ownerAssetName);
        obj.transform.position = Vector3.zero;
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();

        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor character = obj.GetComponent<CharacterActor>();
        InvokeAwake(character);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        return character;
    }

    private static CharacterActor CreateIntruder(string intruderAssetName, Vector3 position)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            $"Assets/Resources/SO/Character/Intruders/{intruderAssetName}.asset");

        GameObject obj = new GameObject(intruderAssetName);
        obj.transform.position = position;
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();

        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor character = obj.GetComponent<CharacterActor>();
        InvokeAwake(character);
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        return character;
    }

    private static void InvokeAwake(CharacterActor character)
    {
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
    }

    private static void ClearShopStock(BuildableObject building)
    {
        if (building is Shop shop)
        {
            shop.DebugClearStock();
        }
    }

    private sealed class CommandScenarioWorld : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        public CommandScenarioWorld(int width = 12, int hallwayCount = 12)
        {
            Grid = new Grid(width, 1);
            for (int x = 0; x < Mathf.Min(width, hallwayCount); x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }
        }

        public Grid Grid { get; }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.Inject(building);
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            return building;
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
