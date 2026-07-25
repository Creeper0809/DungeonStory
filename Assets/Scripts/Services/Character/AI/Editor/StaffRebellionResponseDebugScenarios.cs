using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StaffRebellionResponseDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P2 Staff Rebellion Response Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 staff rebellion response scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("자동 제압 배정", VerifyAutoSuppressAssignment, errors);
        RunScenario("반란 직원 제압 명령 대상", VerifyRebelSuppressCommandTarget, errors);
        RunScenario("격리로 사장 위협 확산 차단", VerifyIsolationBlocksOwnerThreat, errors);
        RunScenario("반란 직전 진정", VerifyCalmBeforeRebellion, errors);
        RunScenario("제압 완료 후 대상 해제", VerifySuppressedRebelClearsThreat, errors);

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
            Debug.Log("P2 staff rebellion response scenarios passed.");
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

    private static bool VerifyAutoSuppressAssignment()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(301, "Auto Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(302, "Auto Rebel", new Vector2Int(3, 0), 5f);

        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome outcome);
        AbilityWork guardWork = guard.GetAbility<AbilityWork>();

        return outcome == StaffDiscontentOutcome.LocalRebellion
            && guardWork.PrioritySuppressActor == CharacterActor.From(rebel)
            && guardWork.HasPrioritySuppressTarget
            && guardWork.TryGetPrioritySuppressDestination(world.Grid.SearchPath(new Vector2Int(0, 0)), out BuildableObject destination)
            && destination != null;
    }

    private static bool VerifyRebelSuppressCommandTarget()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(303, "Manual Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(304, "Manual Rebel", new Vector2Int(2, 0), 5f);

        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        return WorkCommandResolver.IsSuppressTarget(CharacterActor.From(rebel), world.Runtime.IsRebellionTarget)
            && WorkCommandResolver.TryResolveSuppressCommand(
                CharacterActor.From(guard),
                CharacterActor.From(rebel),
                world.Runtime.IsRebellionTarget,
                out _);
    }

    private static bool VerifyIsolationBlocksOwnerThreat()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor rebel = world.CreateStaff(305, "Isolated Rebel", new Vector2Int(2, 0), 5f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        bool isolated = world.Runtime.TryIsolateRebel(CharacterActor.From(rebel), null, out StaffRebellionResponseResult isolationResult);
        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome secondOutcome);
        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome thirdOutcome);

        return record != null
            && isolated
            && isolationResult.Success
            && record.IsIsolated
            && !record.IsOwnerThreat
            && secondOutcome == StaffDiscontentOutcome.None
            && thirdOutcome == StaffDiscontentOutcome.None;
    }

    private static bool VerifyCalmBeforeRebellion()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor staff = world.CreateStaff(306, "Calm Target", new Vector2Int(1, 0), 20f);
        CharacterActor actor = world.CreateStaff(307, "Negotiator", new Vector2Int(0, 0), 80f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome beforeOutcome);
        bool calmed = world.Runtime.TryCalmStaff(CharacterActor.From(staff), CharacterActor.From(actor), out StaffRebellionResponseResult calmResult);

        return record != null
            && beforeOutcome == StaffDiscontentOutcome.WorkDisruption
            && calmed
            && calmResult.Success
            && record.Stage != StaffDiscontentStage.WorkDisruption
            && record.Stage != StaffDiscontentStage.LocalRebellion
            && staff.stats[CharacterCondition.MOOD] > 20f;
    }

    private static bool VerifySuppressedRebelClearsThreat()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(308, "Suppressing Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(309, "Suppressed Rebel", new Vector2Int(2, 0), 5f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        bool resolved = world.Runtime.ResolveSuppressedRebel(CharacterActor.From(rebel), CharacterActor.From(guard));

        return record != null
            && resolved
            && record.IsSuppressed
            && record.IsPermanentLoss
            && !record.IsInLocalRebellion
            && !WorkCommandResolver.IsSuppressTarget(CharacterActor.From(rebel), world.Runtime.IsRebellionTarget);
    }

    private sealed class RebellionScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public RebellionScenarioWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            Grid = new Grid(8, 1);

            GameObject gridSystemObject = new GameObject("Rebellion Response GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);

            GameObject runtimeObject = new GameObject("Rebellion Response Runtime");
            objects.Add(runtimeObject);
            Runtime = runtimeObject.AddComponent<StaffDiscontentRuntime>();
            CharacterAiEditorTestDependencies.Inject(Runtime, objects);

            for (int x = 0; x < Grid.width; x++)
            {
                PlaceHallway(new Vector2Int(x, 0));
            }
        }

        public Grid Grid { get; }
        public StaffDiscontentRuntime Runtime { get; }

        public CharacterActor CreateStaff(int id, string name, Vector2Int position, float mood)
        {
            GameObject obj = new GameObject(name);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            obj.AddComponent<AbilityWork>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj, Runtime);
            CharacterAwakeMethod?.Invoke(character, null);

            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            scriptableObjects.Add(data);
            data.id = id;
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = name;
            data.speciesTag = "Orc";
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            obj.transform.position = Grid.GetWorldPos(position);
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.Identity.SetPersistentId($"staff-rebellion-test:{id}");
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.stats[CharacterCondition.SLEEP] = 50f;
            character.stats[CharacterCondition.HUNGER] = 50f;
            character.stats[CharacterCondition.FUN] = 50f;
            character.stats[CharacterCondition.EXCRETION] = 50f;
            character.stats[CharacterCondition.HYGIENE] = 50f;
            character.stats[CharacterCondition.MOOD] = mood;
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }

        private void PlaceHallway(Vector2Int position)
        {
            GameObject obj = new GameObject($"Hallway {position.x}");
            objects.Add(obj);
            BuildableObject hallway = obj.AddComponent<BuildableObject>();
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            scriptableObjects.Add(data);
            data.id = 7000 + position.x;
            data.objectName = obj.name;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Hallway;
            data.category = BuildingCategory.Movement;
            data.type = typeof(BuildableObject);

            obj.transform.position = Grid.GetWorldPos(position);
            hallway.SetGrid(Grid);
            hallway.Initialization(data, position);
            Grid.RegisterOccupant(
                hallway,
                GridLayer.Hallway,
                data.GetGridPosList(position),
                true);
        }
    }
}
