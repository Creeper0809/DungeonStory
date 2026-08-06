using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using UnityEngine;

internal static class CharacterAiPlanDebugFixtures
{
    public static AiDirectorRuntime GetOrCreatePlayModeAiDirector(out GameObject createdObject)
    {
        createdObject = null;
        AiDirectorRuntime existing = FindPlayModeAiDirector();
        if (existing != null)
        {
            CharacterAiEditorTestDependencies.Inject(existing);
            return existing;
        }

        createdObject = new GameObject("PlayLlmProbeAiDirector", typeof(AiDirectorRuntime));
        AiDirectorRuntime created = createdObject.GetComponent<AiDirectorRuntime>();
        CharacterAiEditorTestDependencies.Inject(created);
        return created;
    }

    public static AiDirectorRuntime FindPlayModeAiDirector()
    {
        AiDirectorRuntime[] directors = Object.FindObjectsByType<AiDirectorRuntime>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (AiDirectorRuntime director in directors)
        {
            if (director != null && director.gameObject.activeInHierarchy)
            {
                return director;
            }
        }

        return null;
    }

    public static string SafeDebugValue(string value, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        string sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        maxLength = Mathf.Max(8, maxLength);
        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized.Substring(0, maxLength);
    }

    public static GameObject CreatePlayActorObject(string name)
    {
        GameObject actorObject = new GameObject(name);
        actorObject.SetActive(false);
        EnsureActorRuntimeComponents(actorObject);
        BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
        tree.StartWhenEnabled = false;
        AIBrain brain = actorObject.GetComponent<AIBrain>();
        brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
        actorObject.SetActive(true);
        CharacterAiEditorTestDependencies.Inject(actorObject);
        CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
        return actorObject;
    }

    public static GameObject CreateActorObject(string name)
    {
        GameObject actorObject = new GameObject(name);
        actorObject.SetActive(false);
        EnsureActorRuntimeComponents(actorObject);
        BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
        tree.StartWhenEnabled = false;
        actorObject.SetActive(true);
        AIBrain brain = actorObject.GetComponent<AIBrain>();
        if (brain != null && brain.availableActions == null)
        {
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
        }

        CharacterAiEditorTestDependencies.Inject(actorObject);
        CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
        return actorObject;
    }

    public static void DestroyProbeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }

    public static GameObject EnsureGridForScenario(out bool created)
    {
        created = false;
        GridSystemManager existing = Object.FindFirstObjectByType<GridSystemManager>();
        if (existing != null && existing.grid != null)
        {
            return existing.gameObject;
        }

        GameObject gridObject = new GameObject("CharacterAiScenarioGridSystem");
        gridObject.SetActive(false);
        GridSystemManager manager = gridObject.AddComponent<GridSystemManager>();
        manager.defaultGridWidth = 4;
        manager.defaultGridHeight = 4;
        gridObject.SetActive(true);
        manager.EnsureGridInitialized();
        created = true;
        return gridObject;
    }

    public static SocialReputationRuntime EnsureSocialRuntimeInstance(out GameObject createdObject)
    {
        createdObject = null;
        SocialReputationRuntime existing = FindSocialRuntimeInstance();
        if (existing != null)
        {
            CharacterAiEditorTestDependencies.Inject(existing);
            typeof(SocialReputationRuntime)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(existing, null);
            return existing;
        }

        createdObject = new GameObject("PlanScenarioSocialReputationRuntime", typeof(SocialReputationRuntime));
        SocialReputationRuntime runtime = createdObject.GetComponent<SocialReputationRuntime>();
        CharacterAiEditorTestDependencies.Inject(runtime);
        typeof(SocialReputationRuntime)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(runtime, null);
        return runtime;
    }

    public static SocialReputationRuntime FindSocialRuntimeInstance()
    {
        SocialReputationRuntime[] runtimes = Object.FindObjectsByType<SocialReputationRuntime>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return runtimes.FirstOrDefault((runtime) => runtime != null && runtime.gameObject.activeInHierarchy)
            ?? runtimes.FirstOrDefault((runtime) => runtime != null);
    }

    public static LocalLlmRequestQueue EnsureQueueInstance(out GameObject createdObject)
    {
        createdObject = null;
        LocalLlmRequestQueue[] existingQueues = Object.FindObjectsByType<LocalLlmRequestQueue>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (existingQueues.Length > 0)
        {
            LocalLlmRequestQueue primary = existingQueues.FirstOrDefault((queue) =>
                queue != null && queue.gameObject.activeInHierarchy)
                ?? existingQueues.FirstOrDefault((queue) => queue != null);
            foreach (LocalLlmRequestQueue duplicate in existingQueues)
            {
                if (duplicate == null || duplicate == primary)
                {
                    continue;
                }

                if (duplicate.name.StartsWith("PlanScenario", System.StringComparison.Ordinal)
                    || duplicate.name == nameof(LocalLlmRequestQueue))
                {
                    Object.DestroyImmediate(duplicate.gameObject);
                }
            }

            if (primary != null)
            {
                CharacterAiEditorTestDependencies.Inject(primary);
                typeof(LocalLlmRequestQueue)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(primary, null);
                return primary;
            }
        }

        createdObject = new GameObject("PlanScenarioLocalLlmQueue", typeof(LocalLlmRequestQueue));
        LocalLlmRequestQueue queue = createdObject.GetComponent<LocalLlmRequestQueue>();
        CharacterAiEditorTestDependencies.Inject(queue);
        typeof(LocalLlmRequestQueue)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(queue, null);
        return queue;
    }

    public static LocalLlmRequestQueue FindQueueInstance()
    {
        LocalLlmRequestQueue[] queues = Object.FindObjectsByType<LocalLlmRequestQueue>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return queues.FirstOrDefault((queue) => queue != null && queue.gameObject.activeInHierarchy)
            ?? queues.FirstOrDefault((queue) => queue != null);
    }

    public static CharacterSO CreateCharacterData(
        CharacterType type,
        string characterName,
        string speciesTag)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.characterType = type;
        data.characterName = characterName;
        data.speciesTag = speciesTag;
        return data;
    }

    public static BuildingSO CreateBuildingData(int id, string objectName)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = id;
        data.objectName = objectName;
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Building;
        data.category = BuildingCategory.Shop;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.Rest,
            capacity = 1,
            disabledWhenDamaged = true
        };
        data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Repair });
        return data;
    }

    private static void EnsureActorRuntimeComponents(GameObject actorObject)
    {
        EnsureLocalComponent<CharacterIdentity>(actorObject);
        EnsureLocalComponent<CharacterAbilityCache>(actorObject);
        EnsureLocalComponent<CharacterStats>(actorObject);
        EnsureLocalComponent<CharacterVisual>(actorObject);
        EnsureLocalComponent<CharacterLifecycle>(actorObject);
        EnsureLocalComponent<CharacterLog>(actorObject);
        EnsureLocalComponent<CharacterBlackboard>(actorObject);
        EnsureLocalComponent<BehaviorTree>(actorObject);
        EnsureLocalComponent<AIBrain>(actorObject);
        EnsureLocalComponent<AbilityMove>(actorObject);
        EnsureLocalComponent<CharacterActor>(actorObject);
        EnsureLocalComponent<CustomerPersonaRuntime>(actorObject);
        EnsureLocalComponent<CharacterDialogueRuntime>(actorObject);
        EnsureLocalComponent<CharacterSocialMemory>(actorObject);
        CharacterIdentity identity = actorObject.GetComponent<CharacterIdentity>();
        if (identity != null && string.IsNullOrWhiteSpace(identity.PersistentId))
        {
            identity.SetPersistentId($"character:plan-debug:{SanitizeId(actorObject.name)}");
        }

        CharacterAiEditorTestDependencies.Inject(actorObject);
    }

    private static T EnsureLocalComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "actor";
        }

        return new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');
    }
}
